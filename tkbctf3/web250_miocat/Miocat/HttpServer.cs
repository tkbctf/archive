using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using System.Globalization;

namespace Miocat
{
    class HttpServer
    {
        private readonly HttpListener listener;
        public bool IsListening
        {
            get { return listener.IsListening; }
        }

        public HttpServer(int port)
        {
            if (port <= 0)
                throw new ArgumentException("port must be greater than 0.", "port");

            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + port + "/");
        }

        public void Start()
        {
            Console.WriteLine("Start Listening");
            listener.Start();
            Task.Factory.StartNew(Listen);
        }

        private void Listen()
        {
            while (IsListening)
            {
                var context = listener.GetContext();
                Console.WriteLine("Accept connection");
                Task.Factory.StartNew(HandleRequest, context);
            }
        }

        private void HandleRequest(object ctx)
        {
            var context = (HttpListenerContext)ctx;
            var request = context.Request;
            var requestUri = request.Url;

            if (requestUri.AbsolutePath == "/")
            {
                var helloHtml = "<!DOCTYPE html>" + Environment.NewLine
                                + "<html>" + Environment.NewLine
                                + "<head>" + Environment.NewLine
                                + "  <title>miocat - accelerate your development</title>" + Environment.NewLine
                                + "  <meta charset=\"UTF-8\">" + Environment.NewLine
                                + "</head>" + Environment.NewLine
                                + "<body>" + Environment.NewLine
                                + "  <h1>" + Environment.NewLine
                                + "    miocat" + Environment.NewLine
                                + "    <small>Accelarate Your Development</small>" + Environment.NewLine
                                + "  </h1>" + Environment.NewLine
                                + "  <form action=\"/request\">" + Environment.NewLine
                                + "    <input type=\"text\" name=\"target\" placeholder=\"Target URL\">" + Environment.NewLine
                                + "<input type=\"hidden\" name=\"locale\" value=\"en-US\">" + Environment.NewLine
                                + "    <button type=\"submit\">nya!</button>" + Environment.NewLine
                                + "  </form>" + Environment.NewLine
                                + "</body>" + Environment.NewLine
                                + "</html>";
                GenerateResponse(context.Response, 200, helloHtml);
            }
            else if (requestUri.AbsolutePath == "/request")
            {
                ProcessRequest(context, requestUri);
            }
            else
            {
                GenerateResponse(context.Response, 404, "Unknown route.");
            }
        }

        private static void ProcessRequest(HttpListenerContext context, Uri requestUri)
        {
            var query = ParseQuery(requestUri.Query);
            Console.WriteLine("Requested {0} with:", requestUri.ToString());
            foreach (var kv in query)
            {
                Console.WriteLine("  {0}: {1}", kv.Key, kv.Value);
            }
            var culture = GetLocale(query);
            if (query.ContainsKey("target"))
            {
                var target = query["target"].Trim();
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                if (string.IsNullOrWhiteSpace(target))
                {
                    GenerateResponse(context.Response, 500, "empty target.");
                    return;
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(target, "^[a-zA-Z]+://.*$"))
                {
                    Console.WriteLine("not acceptable: no schema");
                    GenerateResponse(context.Response, 500, "not acceptable.");
                    return;
                }
                if (IsFileUri(target))
                {
                    Console.WriteLine("not acceptable: file schema");
                    GenerateResponse(context.Response, 500, "not acceptable.");
                    return;
                }
                var client = new WebClient();
                try
                {
                    // client.Proxy = new WebProxy("88.255.234.109", 3128);
                    client.Headers.Add(
                        HttpRequestHeader.AcceptLanguage,
                        System.Threading.Thread.CurrentThread.CurrentCulture.Name + ",en-US");
                    var content = client.DownloadString(target);
                    var regex = new Regex("<title>(.+)</title>");
                    if (regex.IsMatch(content))
                    {
                        content = regex.Replace(content, "<title>(miocat) $1</title>");
                    }
                    else
                    {
                        var headRegex = new Regex("</head>");
                        content = headRegex.Replace(content, "<title>(miocat) (no title)</title>\n</head>");
                    }
                    GenerateResponse(context.Response, 200, content);
                }
                catch (WebException e)
                {
                    GenerateResponse(context.Response, 500, e.Message);
                }
            }
            else
            {
                GenerateResponse(context.Response, 500, "no target specified.");
            }
        }

        private static void GenerateResponse(HttpListenerResponse response, int statusCode, string body)
        {
            response.StatusCode = statusCode;
            var buffer = System.Text.Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static CultureInfo GetLocale(IDictionary<string, string> query)
        {
            var culture = CultureInfo.InvariantCulture;
            if (query.ContainsKey("locale"))
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(query["locale"]);
                }
                catch (CultureNotFoundException)
                {
                    Console.Error.WriteLine("Locale Not Found");
                    // do nothing, so if the culture is not found, the invariant culture is used.
                    query.Remove("locale");
                }
            }
            return culture;
        }

        private static IDictionary<string, string> ParseQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new Dictionary<string, string>();

            // skips '?', split by '&', and split by '='.
            return query.Substring(1).Split('&')
                .Select(s => s.Split('='))
                .ToDictionary(arr => arr[0], arr => Uri.UnescapeDataString(arr[1]));
        }

        private static bool IsFileUri(string uri)
        {
            return uri.StartsWith("FILE:", true, System.Threading.Thread.CurrentThread.CurrentCulture);
        }

        private static bool IsHttpUri(string uri)
        {
            return uri.StartsWith("HTTP:", true, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsHttpsUri(string uri)
        {
            return uri.StartsWith("HTTPS:", true, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
