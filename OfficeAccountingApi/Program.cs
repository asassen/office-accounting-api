using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.BusinessSolutions.SmallBusinessAccounting;

namespace OfficeAccountingApi
{
    static class Json
    {
        static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static string Write(object o) => Serializer.Serialize(o);
        public static Dictionary<string, object> ReadObject(string s) =>
            Serializer.DeserializeObject(s) as Dictionary<string, object>;
    }

    static class Mapper
    {
        public static List<Dictionary<string, object>> Rows(DataView view)
        {
            var list = new List<Dictionary<string, object>>(view.Count);
            foreach (DataRowView row in view)
            {
                var d = new Dictionary<string, object>();
                foreach (DataColumn c in view.Table.Columns)
                    d[c.ColumnName] = Convert(row[c.ColumnName]);
                list.Add(d);
            }
            return list;
        }

        static object Convert(object v)
        {
            if (v == null || v is DBNull) return null;
            if (v is Guid || v is Enum) return v.ToString();
            if (v is DateTime dt) return dt.ToString("o");
            if (v is byte[]) return null;
            if (v is decimal || v is int || v is long || v is bool || v is string || v is double || v is short || v is float) return v;
            return v.ToString();
        }

        public static Dictionary<string, object> Customer(ICustomerAccount c)
        {
            var contact = c.GetPrimaryContact();
            return new Dictionary<string, object>
            {
                ["guid"] = c.Guid.ToString(),
                ["accountId"] = c.AccountID,
                ["displayNumber"] = c.DisplayNumber,
                ["name"] = c.Name,
                ["active"] = c.Active,
                ["suspended"] = c.IsSuspended,
                ["comments"] = c.Comments,
                ["email"] = contact?.Email,
                ["phone"] = contact?.Phone,
                ["balance"] = c.TotalBalance,
                ["overdue"] = c.Overdue,
                ["salesYtd"] = c.SalesYTD,
            };
        }

        public static Dictionary<string, object> Invoice(ISalesInvoice inv)
        {
            var lines = new List<Dictionary<string, object>>();
            decimal net = 0m, tax = 0m;
            foreach (var row in Rows(inv.SalesInvoiceLines.DataView))
            {
                var sub = row.TryGetValue("SubTotal", out var st) && st != null ? System.Convert.ToDecimal(st) : 0m;
                if (row.TryGetValue("LineItemID", out var li) && li != null)
                {
                    net += sub;
                    lines.Add(new Dictionary<string, object>
                    {
                        ["itemName"] = row["ItemName"],
                        ["description"] = row["Description"],
                        ["quantity"] = row["Quantity"],
                        ["unitPrice"] = row["UnitPrice"],
                        ["subTotal"] = sub,
                    });
                }
                else if (row.TryGetValue("TaxCodeID", out var tc) && tc != null) tax += sub;
            }
            return new Dictionary<string, object>
            {
                ["guid"] = inv.Guid.ToString(),
                ["number"] = inv.DocumentDisplayNumber,
                ["reference"] = inv.ReferenceNumber,
                ["customerGuid"] = inv.CustomerAccount?.Guid.ToString(),
                ["customerName"] = inv.CustomerName,
                ["date"] = inv.FinancialDateTime.ToString("o"),
                ["dueDate"] = inv.DueDate.ToString("o"),
                ["subTotal"] = net,
                ["tax"] = tax,
                ["total"] = net + tax,
                ["balance"] = inv.Balance,
                ["voided"] = inv.IsVoided,
                ["lines"] = lines,
            };
        }

        public static ISalesInvoice CreateInvoice(ISmallBusinessInstance i, Dictionary<string, object> body)
        {
            if (!body.TryGetValue("customerGuid", out var cg) || !Guid.TryParse(cg as string, out var customerGuid))
                throw new ApiException(400, "customerGuid is required");
            var customer = i.CustomerAccounts.GetByGuid(customerGuid) as ICustomerAccount
                           ?? throw new ApiException(404, "Customer not found");
            var lines = body.TryGetValue("lines", out var l) ? l as System.Collections.IList : null;
            if (lines == null || lines.Count == 0) throw new ApiException(400, "lines must contain at least one line");

            var inv = i.CreateSalesInvoice(false);
            inv.CustomerAccount = customer;
            if (body.TryGetValue("date", out var date) && date != null) inv.FinancialDateTime = DateTime.Parse((string)date);
            if (body.TryGetValue("dueDate", out var due) && due != null) inv.DueDate = DateTime.Parse((string)due);
            if (body.TryGetValue("reference", out var reference) && reference != null) inv.ReferenceNumber = (string)reference;

            foreach (var raw in lines)
            {
                var ln = raw as Dictionary<string, object> ?? throw new ApiException(400, "Invalid line");
                if (!ln.TryGetValue("itemGuid", out var ig) || !Guid.TryParse(ig as string, out var itemGuid))
                    throw new ApiException(400, "Each line needs itemGuid");
                var item = i.ItemAccounts.GetByGuid(itemGuid) as IItemAccount
                           ?? throw new ApiException(404, "Item not found: " + itemGuid);
                var line = (IItemLine)inv.CreateSalesInvoiceLine(DocumentLineType.ContractItemLineType);
                line.LineItem = item;
                line.Quantity = ln.TryGetValue("quantity", out var q) ? System.Convert.ToDecimal(q) : 1m;
                if (ln.TryGetValue("unitPrice", out var p) && p != null) line.UnitPrice = System.Convert.ToDecimal(p);
                if (ln.TryGetValue("description", out var d) && d != null) line.Description = (string)d;
            }
            inv.Save();
            return inv;
        }

        public static void ApplyCustomer(ICustomerAccount c, Dictionary<string, object> body)
        {
            if (body.TryGetValue("name", out var name)) c.Name = (string)name;
            if (body.TryGetValue("comments", out var comments)) c.Comments = (string)comments;
            if (body.TryGetValue("active", out var active)) c.Active = System.Convert.ToBoolean(active);

            body.TryGetValue("email", out var email);
            body.TryGetValue("phone", out var phone);
            if (email != null || phone != null)
            {
                var contact = c.GetPrimaryContact() ?? c.CreateContact();
                if (string.IsNullOrEmpty(contact.Name)) contact.Name = c.Name;
                if (email != null) contact.Email = (string)email;
                if (phone != null) contact.Phone = (string)phone;
            }
        }
    }

    class ApiException : Exception
    {
        public int Status { get; }
        public ApiException(int status, string message) : base(message) { Status = status; }
    }

    static class Log
    {
        static readonly object Gate = new object();
        static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OfficeAccountingApi", "log.txt");

        public static void Write(string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message;
            if (Environment.UserInteractive) Console.WriteLine(line);
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    File.AppendAllText(Path, line + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    sealed class SbaService : System.ServiceProcess.ServiceBase
    {
        public const string Name = "OfficeAccountingApi";
        public SbaService() { ServiceName = Name; }

        protected override void OnStart(string[] args)
        {
            // Connecting can take a while; do it off the SCM thread and fail the service if it can't.
            new System.Threading.Thread(() =>
            {
                try { Program.Start(); }
                catch (Exception ex)
                {
                    Log.Write("Startup failed: " + ex.GetBaseException().Message);
                    Environment.Exit(1);
                }
            }) { IsBackground = true }.Start();
        }

        protected override void OnStop() => Program.Stop();
    }

    /// <summary>Opens each company on first use and keeps it open. Only databases that exist on the instance are accepted.</summary>
    sealed class Companies : IDisposable
    {
        readonly string server;
        readonly string defaultCompany;
        readonly Dictionary<string, SbaSession> open = new Dictionary<string, SbaSession>(StringComparer.OrdinalIgnoreCase);
        List<string> known;

        public Companies(string server, string defaultCompany) { this.server = server; this.defaultCompany = defaultCompany; }

        public string Default => defaultCompany;

        public List<string> List(bool refresh = false)
        {
            if (known != null && !refresh) return known;
            var names = new List<string>();
            var cs = $"Server={server};Integrated Security=true;Connect Timeout=10";
            using (var conn = new System.Data.SqlClient.SqlConnection(cs))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "select name from sys.databases where database_id > 4 " +
                                  "and object_id(quotename(name) + '.dbo.VersionInfoTable') is not null order by name";
                using (var r = cmd.ExecuteReader()) while (r.Read()) names.Add(r.GetString(0));
            }
            return known = names;
        }

        public SbaSession Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = defaultCompany;
            var match = List().Find(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
                        ?? List(true).Find(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ApiException(404, "Unknown company: " + name);

            if (open.TryGetValue(match, out var s)) return s;
            Log.Write("Opening " + match);
            s = new SbaSession(null, match, server);
            try { s.WaitUntilReady().GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                s.Dispose();
                throw new ApiException(503, $"Cannot open company '{match}': {ex.GetBaseException().Message}");
            }
            open[match] = s;
            return s;
        }

        public void Dispose() { foreach (var s in open.Values) s.Dispose(); open.Clear(); }
    }

    static class Program
    {
        static Companies companies;
        // Requests are handled one at a time, so a single "current" session is safe.
        static SbaSession session;
        static string apiKey;
        static HttpListener listener;

        static void Main()
        {
            if (Environment.UserInteractive)
            {
                try { Start(); }
                catch (Exception ex)
                {
                    Log.Write("Startup failed: " + ex.GetBaseException().Message);
                    Environment.Exit(1);
                }
                Console.WriteLine("Press Enter to stop.");
                Console.ReadLine();
                Stop();
            }
            else
            {
                System.ServiceProcess.ServiceBase.Run(new SbaService());
            }
        }

        public static void Start()
        {
            var settings = System.Configuration.ConfigurationManager.AppSettings;
            apiKey = settings["ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "CHANGE-ME")
                throw new InvalidOperationException("Set a real ApiKey in OfficeAccountingApi.exe.config.");

            var server = settings["Server"];
            if (string.IsNullOrWhiteSpace(server)) server = @".\MSSMLBIZ";
            companies = new Companies(server, settings["Database"]);
            Log.Write("Companies on " + server + ": " + string.Join(", ", companies.List()));
            if (!string.IsNullOrWhiteSpace(companies.Default)) companies.Get(null);

            listener = new HttpListener();
            listener.Prefixes.Add(settings["Prefix"]);
            listener.Start();
            Log.Write("Listening on " + settings["Prefix"]);

            new System.Threading.Thread(Loop) { IsBackground = true, Name = "http" }.Start();
        }

        public static void Stop()
        {
            try { listener?.Close(); } catch { }
            companies?.Dispose();
            Log.Write("Stopped");
        }

        static void Loop()
        {
            // One request at a time: the SDK thread serialises anyway, and this keeps the code simple.
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch { break; }
                Handle(ctx);
            }
        }

        static void Handle(HttpListenerContext ctx)
        {
            int status = 200;
            object result;
            try
            {
                if (!KeyOk(ctx.Request.Headers["X-Api-Key"])) throw new ApiException(401, "Missing or invalid X-Api-Key");
                result = Route(ctx.Request);
            }
            catch (ApiException ex) { status = ex.Status; result = new { error = ex.Message }; }
            catch (Exception ex) { status = 500; result = new { error = ex.GetBaseException().Message }; }

            var bytes = Encoding.UTF8.GetBytes(Json.Write(result));
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
            Log.Write($"{ctx.Request.HttpMethod} {ctx.Request.Url.AbsolutePath} -> {status}");
        }

        static bool KeyOk(string supplied)
        {
            if (supplied == null || supplied.Length != apiKey.Length) return false;
            int diff = 0;
            for (int i = 0; i < apiKey.Length; i++) diff |= supplied[i] ^ apiKey[i];
            return diff == 0;
        }

        static object Route(HttpListenerRequest req)
        {
            var parts = req.Url.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2 || parts[0] != "api") throw new ApiException(404, "Not found");
            var resource = parts[1].ToLowerInvariant();
            var id = parts.Length > 2 ? parts[2] : null;
            var method = req.HttpMethod.ToUpperInvariant();

            if (resource == "companies")
                return new { @default = companies.Default, companies = companies.List(true) };

            session = companies.Get(req.Headers["X-Company"] ?? req.QueryString["company"]);

            switch (resource)
            {
                case "health":
                    return new { status = "ok", company = session.Invoke(i => i.CompanyName).GetAwaiter().GetResult() };

                case "customers":
                    return Customers(req, method, id);

                case "vendors": return List(method, i => i.VendorAccounts.DataView);
                case "items": return List(method, i => i.ItemAccounts.DataView);
                case "invoices": return Invoices(req, method, id);
                default: throw new ApiException(404, "Unknown resource");
            }
        }

        static object List(string method, Func<ISmallBusinessInstance, DataView> view)
        {
            if (method != "GET") throw new ApiException(405, "Method not allowed");
            return session.Invoke(i => Mapper.Rows(view(i))).GetAwaiter().GetResult();
        }

        static object Customers(HttpListenerRequest req, string method, string id)
        {
            if (id == null && method == "GET") return List(method, i => i.CustomerAccounts.DataView);

            if (id == null && method == "POST")
            {
                var body = ReadBody(req);
                if (!body.ContainsKey("name")) throw new ApiException(400, "name is required");
                return session.Invoke(i =>
                {
                    var c = i.CreateCustomerAccount();
                    Mapper.ApplyCustomer(c, body);
                    c.Save();
                    return Mapper.Customer(c);
                }).GetAwaiter().GetResult();
            }

            if (id != null && Guid.TryParse(id, out var guid))
            {
                if (method == "GET")
                    return session.Invoke(i => Mapper.Customer(Find(i, guid))).GetAwaiter().GetResult();

                if (method == "PUT")
                {
                    var body = ReadBody(req);
                    return session.Invoke(i =>
                    {
                        var c = Find(i, guid);
                        Mapper.ApplyCustomer(c, body);
                        c.Save();
                        return Mapper.Customer(c);
                    }).GetAwaiter().GetResult();
                }
            }
            throw new ApiException(405, "Method not allowed");
        }

        static object Invoices(HttpListenerRequest req, string method, string id)
        {
            if (id == null && method == "GET") return List(method, i => i.SalesInvoices.DataView);

            if (id == null && method == "POST")
            {
                var body = ReadBody(req);
                return session.Invoke(i => Mapper.Invoice(Mapper.CreateInvoice(i, body))).GetAwaiter().GetResult();
            }

            if (id != null && method == "GET" && Guid.TryParse(id, out var guid))
                return session.Invoke(i => Mapper.Invoice(
                    i.SalesInvoices.GetByGuid(guid) as ISalesInvoice ?? throw new ApiException(404, "Invoice not found")))
                    .GetAwaiter().GetResult();

            throw new ApiException(405, "Method not allowed");
        }

        static ICustomerAccount Find(ISmallBusinessInstance i, Guid guid) =>
            i.CustomerAccounts.GetByGuid(guid) as ICustomerAccount
            ?? throw new ApiException(404, "Customer not found");

        static Dictionary<string, object> ReadBody(HttpListenerRequest req)
        {
            if (req.ContentLength64 > 1_000_000) throw new ApiException(413, "Body too large");
            using (var r = new StreamReader(req.InputStream, Encoding.UTF8))
                return Json.ReadObject(r.ReadToEnd()) ?? throw new ApiException(400, "Invalid JSON body");
        }
    }
}
