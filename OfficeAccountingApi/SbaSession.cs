using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BusinessSolutions.SmallBusinessAccounting;
using Microsoft.BusinessSolutions.SmallBusinessAccounting.Loader;

namespace OfficeAccountingApi
{
    /// <summary>
    /// Owns the SDK connection. Every SDK call is marshalled onto one STA thread
    /// because the object model is not safe for concurrent use.
    /// </summary>
    public sealed class SbaSession : IDisposable
    {
        const string LoaderFullName = "Loader, Version=2.0.5201.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        const string LoaderTypeName = "Microsoft.BusinessSolutions.SmallBusinessAccounting.Loader.Loader";

        static readonly string[] ProbeDirs =
        {
            @"C:\Program Files (x86)\Microsoft Small Business\Office Accounting 2009",
            @"C:\Program Files (x86)\Microsoft Small Business\Office Accounting 2009\Assemblies",
            @"C:\Program Files (x86)\Microsoft Small Business\Small Business Loader",
        };

        readonly string companyFile;
        readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        readonly Thread thread;
        readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
        ISmallBusinessInstance instance;

        readonly string database;
        readonly string server;

        // database/server override the .sbc file, whose stored server name goes stale when the PC is renamed.
        public SbaSession(string companyFile, string database, string server)
        {
            this.companyFile = ResolveCompanyFile(companyFile ?? "");
            this.database = database;
            this.server = server;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromInstall;
            thread = new Thread(Run) { IsBackground = true, Name = "SBA-STA" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public string CompanyFile => string.IsNullOrWhiteSpace(database) ? companyFile : database + " on " + server;
        public Task WaitUntilReady() => ready.Task;

        public Task<T> Invoke<T>(Func<ISmallBusinessInstance, T> work)
        {
            var tcs = new TaskCompletionSource<T>();
            queue.Add(() =>
            {
                try { tcs.SetResult(work(instance)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        void Run()
        {
            try
            {
                instance = Connect();
                ready.SetResult(true);
            }
            catch (Exception ex)
            {
                ready.SetException(ex);
                return;
            }

            foreach (var job in queue.GetConsumingEnumerable()) job();

            try { instance.LogOff(); } catch { }
        }

        ISmallBusinessInstance Connect()
        {
            bool direct = !string.IsNullOrWhiteSpace(database) && !string.IsNullOrWhiteSpace(server);
            if (!direct && !File.Exists(companyFile))
                throw new FileNotFoundException("Company file not found", companyFile);

            Assembly loaderAssembly;
            try { loaderAssembly = Assembly.Load(LoaderFullName); }
            catch (FileNotFoundException) { loaderAssembly = Assembly.LoadFrom(Path.Combine(ProbeDirs[2], "Loader.dll")); }

            var loader = (ILoader)loaderAssembly.CreateInstance(LoaderTypeName);
            var objects = direct ? loader.GetSbaObjects(database, server) : loader.GetSbaObjects(companyFile);
            if (objects == null || objects.SmallBusinessInstance == null)
                throw new InvalidOperationException("Could not attach to the company database. Is SQL Server (MSSMLBIZ) running?");
            return (ISmallBusinessInstance)objects.SmallBusinessInstance;
        }

        static string ResolveCompanyFile(string file)
        {
            if (file.Contains(Path.DirectorySeparatorChar.ToString())) return file;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Small Business Accounting", "Companies", file);
        }

        static Assembly ResolveFromInstall(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            foreach (var dir in ProbeDirs)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }

        public void Dispose() => queue.CompleteAdding();
    }
}
