using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace ShotAsk
{
    internal sealed class AskClient
    {
        private readonly AppConfig _config;
        private readonly object _gate = new object();
        private HttpWebRequest _current;
        private bool _cancelled;

        public AskClient(AppConfig config)
        {
            _config = config;
        }

        public Action<string> OnDelta;
        public Action OnDone;
        public Action<string> OnError;

        public bool Busy { get { return _current != null; } }

        public void Cancel()
        {
            lock (_gate)
            {
                _cancelled = true;
                if (_current != null)
                {
                    try { _current.Abort(); } catch (Exception) { }
                }
            }
        }

        public void Ask(string imageBase64, string mediaType, string question, List<KeyValuePair<string, string>> history)
        {
            Cancel();
            var serializer = new JavaScriptSerializer();
            var payload = new Dictionary<string, object>();
            payload["image"] = imageBase64;
            payload["mediaType"] = mediaType;
            payload["question"] = question;
            payload["history"] = history ?? new List<KeyValuePair<string, string>>();
            string body = serializer.Serialize(payload);
            string endpoint = _config.Endpoint;

            ThreadPool.QueueUserWorkItem(delegate { Run(endpoint, body); });
        }

        private void Run(string endpoint, string body)
        {
            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Accept = "text/event-stream";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 180000;
                request.KeepAlive = false;
                request.Proxy = null;
                request.AllowReadStreamBuffering = false;
                lock (_gate)
                {
                    _cancelled = false;
                    _current = request;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentLength = bytes.Length;
                using (Stream upload = request.GetRequestStream())
                {
                    upload.Write(bytes, 0, bytes.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        string error = reader.ReadToEnd();
                        RaiseError(ExtractError(error, (int)response.StatusCode));
                        return;
                    }

                    string eventName = null;
                    var data = new StringBuilder();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0)
                        {
                            Dispatch(eventName, data.ToString());
                            eventName = null;
                            data.Length = 0;
                            continue;
                        }
                        if (line.StartsWith(":")) continue;
                        if (line.StartsWith("event:", StringComparison.Ordinal))
                        {
                            eventName = line.Substring(6).Trim();
                        }
                        else if (line.StartsWith("data:", StringComparison.Ordinal))
                        {
                            data.Append(line.Substring(5).TrimStart());
                        }
                    }
                }
            }
            catch (WebException error)
            {
                if (IsCancellation(error)) return;
                string message = error.Message;
                try
                {
                    if (error.Response != null)
                    {
                        using (var reader = new StreamReader(error.Response.GetResponseStream()))
                        {
                            message = ExtractError(reader.ReadToEnd(), 0);
                        }
                    }
                }
                catch (Exception) { }
                RaiseError("连接 DSH 失败：" + message);
            }
            catch (Exception error)
            {
                if (IsCancellation(error)) return;
                RaiseError(error.Message);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_current, request)) _current = null;
                }
            }
        }

        private bool IsCancellation(Exception error)
        {
            if (_cancelled) return true;
            var web = error as WebException;
            if (web != null && web.Status == WebExceptionStatus.RequestCanceled) return true;
            if (error is ObjectDisposedException) return true;
            if (error is IOException && _cancelled) return true;
            return false;
        }

        private void Dispatch(string eventName, string data)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (eventName == "delta")
            {
                string text = ExtractText(data);
                if (!string.IsNullOrEmpty(text) && OnDelta != null) OnDelta(text);
            }
            else if (eventName == "done")
            {
                if (OnDone != null) OnDone();
            }
            else if (eventName == "error")
            {
                RaiseError(ExtractMessage(data));
            }
        }

        private void RaiseError(string message)
        {
            if (OnError != null) OnError(message);
        }

        private static string ExtractText(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var value = serializer.Deserialize<Dictionary<string, object>>(json);
                object text;
                if (value != null && value.TryGetValue("text", out text) && text != null) return Convert.ToString(text);
            }
            catch (Exception) { }
            return string.Empty;
        }

        private static string ExtractMessage(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var value = serializer.Deserialize<Dictionary<string, object>>(json);
                object message;
                if (value != null && value.TryGetValue("message", out message) && message != null) return Convert.ToString(message);
            }
            catch (Exception) { }
            return json;
        }

        private static string ExtractError(string body, int status)
        {
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    string message = ExtractMessage(body);
                    if (!string.IsNullOrEmpty(message) && message != body) return message;
                }
                catch (Exception) { }
                if (body.Length < 400) return body;
            }
            return "HTTP " + status;
        }

        public static string Ping(string endpoint, out string model)
        {
            model = null;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "GET";
                request.Timeout = 3000;
                request.ReadWriteTimeout = 3000;
                request.Proxy = null;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    var value = serializer.Deserialize<Dictionary<string, object>>(json);
                    if (value != null)
                    {
                        object provider, modelValue;
                        value.TryGetValue("provider", out provider);
                        value.TryGetValue("model", out modelValue);
                        model = (provider == null ? "" : Convert.ToString(provider)) + " / " + (modelValue == null ? "" : Convert.ToString(modelValue));
                    }
                    return null;
                }
            }
            catch (Exception error)
            {
                return error.Message;
            }
        }
    }
}
