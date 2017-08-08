using System;
using Oxide.Plugins;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Globalization;
using Oxide.Core;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;
using Ping = System.Net.NetworkInformation.Ping;
using System.Collections;

namespace RustyCore.Utils
{
    public static class AntiGateUtils
    {
        static PluginTimers timer = new PluginTimers(null);
        private const string Host = "api.anti-captcha.com";
        private const string ClientKey = ""; // Your key
        private static WebClient webClient = new WebClient();

        static IEnumerator GetCaptcha(string url, Action<string> callback)
        {
            WWW www = new WWW(url);
            yield return www;

            try
            {
                string base64String = Convert.ToBase64String(www.bytes);
                
                var task = AnticaptchaApiWrapper.CreateImageToTextTask(
                    Host,
                    ClientKey,
                    base64String
                );

                if (task == null)
                {
                    Interface.Oxide.RootLogger.Write(LogType.Info, "Somehow task is NULL...");
                }
                else
                {
                    if (task.GetErrorDescription() != null && task.GetErrorDescription().Length > 0)
                    {
                        Interface.Oxide.RootLogger.Write(LogType.Info,
                            "Unfortunately we got the following error from the API: " +
                            task.GetErrorDescription());
                    }
                    CommunityEntity.ServerInstance.StartCoroutine(ProcessTask(task, callback));
                }
            }
            catch (Exception e)
            {
                Interface.Oxide.RootLogger.Write(LogType.Info,
                    "NoCaptcha task (proxyless) failed with following error: " + e.Message + "\n" + e.StackTrace);
            }

        }

        public static void GetAnswer(string url, Action<string> callback)
        {
            CommunityEntity.ServerInstance.StartCoroutine(GetCaptcha(url, callback));
        }

        private static IEnumerator ProcessTask(AnticaptchaTask task, Action<string> callback)
        {
            AnticaptchaResult response;

            do
            {
                response = AnticaptchaApiWrapper.GetTaskResult(Host, ClientKey, task);

                if (response == null || response.GetStatus().Equals(AnticaptchaResult.Status.ready))
                {
                    break;
                }

                if (response.GetStatus().Equals(AnticaptchaResult.Status.processing))
                {
                    yield return new WaitForSeconds(1f);
                }
            } while (response.GetStatus().Equals(AnticaptchaResult.Status.processing));

            if (response?.GetSolution() == null)
            {
                Interface.Oxide.RootLogger.Write(LogType.Info, "Unfortunately we got the following error from the API: " +
                                  (response != null ? response.GetErrorDescription() : "/empty/"));
            }
            else
            {
                Interface.Oxide.NextTick(() => callback(response.GetSolution()));
            }
        }

        #region AnticaptchaTask

        public class AnticaptchaTask
        {
            private readonly string _errorCode;
            private readonly string _errorDescription;
            private readonly int? _errorId;
            private readonly int? _taskId;

            public AnticaptchaTask(int? taskId, int? errorId, string errorCode, string errorDescription)
            {
                _errorId = errorId;
                _taskId = taskId;
                _errorCode = errorCode;
                _errorDescription = errorDescription;
            }

            public string GetErrorCode()
            {
                return _errorCode;
            }

            public string GetErrorDescription()
            {
                return _errorDescription;
            }

            public int? GetTaskId()
            {
                return _taskId;
            }

            public int? GetErrorId()
            {
                return _errorId;
            }

            public override string ToString()
            {
                return "AnticaptchaTask{" +
                       "errorId=" + _errorId +
                       ", taskId=" + _taskId +
                       ", errorCode='" + _errorCode + '\'' +
                       ", errorDescription='" + _errorDescription + '\'' +
                       '}';
            }
        }

        #endregion

        #region AnticaptchaResult

        public class AnticaptchaResult
        {
            public enum Status
            {
                ready,
                unknown,
                processing
            }

            private readonly double? _cost;
            private readonly int? _createTime;
            private readonly int? _endTime;
            private readonly string _errorCode;
            private readonly string _errorDescription;
            private readonly int? _errorId;
            private readonly string _ip;
            private readonly string _solution;
            private readonly int? _solveCount;
            private readonly Status? _status;

            public AnticaptchaResult(Status? status, string solution, int? errorId, string errorCode,
                string errorDescription,
                double? cost, string ip, int? createTime, int? endTime, int? solveCount)
            {
                _errorId = errorId;
                _errorCode = errorCode;
                _errorDescription = errorDescription;
                _status = status;
                _solution = solution;
                _cost = cost;
                _ip = ip;
                _createTime = createTime;
                _endTime = endTime;
                _solveCount = solveCount;
            }

            public override string ToString()
            {
                return "AnticaptchaResult{" +
                       "errorId=" + _errorId +
                       ", errorCode='" + _errorCode + '\'' +
                       ", errorDescription='" + _errorDescription + '\'' +
                       ", status=" + _status +
                       ", solution='" + _solution + '\'' +
                       ", cost=" + _cost +
                       ", ip='" + _ip + '\'' +
                       ", createTime=" + _createTime +
                       ", endTime=" + _endTime +
                       ", solveCount=" + _solveCount +
                       '}';
            }

            public int? GetErrorId()
            {
                return _errorId;
            }

            public string GetErrorCode()
            {
                return _errorCode;
            }

            public string GetErrorDescription()
            {
                return _errorDescription;
            }

            public Status? GetStatus()
            {
                return _status;
            }

            public string GetSolution()
            {
                return _solution;
            }

            public double? GetCost()
            {
                return _cost;
            }

            public string GetIp()
            {
                return _ip;
            }

            public int? GetCreateTime()
            {
                return _createTime;
            }

            public int? GetEndTime()
            {
                return _endTime;
            }

            public int? GetSolveCount()
            {
                return _solveCount;
            }
        }

        #endregion

        #region AnticaptchaApiWrapper

        public class AnticaptchaApiWrapper
        {
            public enum ProxyType
            {
                http
            }

            public static Dictionary<string, bool> HostsChecked = new Dictionary<string, bool>();

            public static bool CheckHost(string host)
            {
                if (!HostsChecked.ContainsKey(host))
                {
                    HostsChecked[host] = Ping(host);
                }

                return HostsChecked[host];
            }
            
            private static JObject JsonPostRequest(string host, string methodName, string postData)
            {
                return HttpHelper.Post(new Uri("http://" + host + "/" + methodName), postData);
            }

            public static bool Ping(string host)
            {
                try
                {
                    new Ping().Send(host, 1000);

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public static AnticaptchaTask CreateNoCaptchaTaskProxyless(string host, string clientKey, string websiteUrl,
                string websiteKey, string userAgent, string websiteSToken = "")
            {
                return CreateNoCaptchaTask(
                    "NoCaptchaTaskProxyless",
                    host,
                    clientKey,
                    websiteUrl,
                    websiteKey,
                    null,
                    null,
                    null,
                    null,
                    null,
                    userAgent,
                    websiteSToken
                    );
            }

            public static AnticaptchaTask CreateNoCaptchaTask(string host, string clientKey, string websiteUrl,
                string websiteKey, ProxyType proxyType, string proxyAddress, int proxyPort, string proxyLogin,
                string proxyPassword, string userAgent, string websiteSToken = "")
            {
                return CreateNoCaptchaTask(
                    "NoCaptchaTask",
                    host,
                    clientKey,
                    websiteUrl,
                    websiteKey,
                    proxyType,
                    proxyAddress,
                    proxyPort,
                    proxyLogin,
                    proxyPassword,
                    userAgent,
                    websiteSToken
                    );
            }

            private static AnticaptchaTask CreateNoCaptchaTask(
                string type,
                string host,
                string clientKey,
                string websiteUrl,
                string websiteKey,
                ProxyType? proxyType,
                string proxyAddress,
                int? proxyPort,
                string proxyLogin,
                string proxyPassword,
                string userAgent,
                string websiteSToken = ""
                )
            {
                if (proxyType != null && (string.IsNullOrEmpty(proxyAddress) || !CheckHost(proxyAddress)))
                {
                    throw new Exception("Proxy address is incorrect!");
                }

                if (proxyType != null && (proxyPort < 1 || proxyPort > 65535))
                {
                    throw new Exception("Proxy port is incorrect!");
                }

                if (string.IsNullOrEmpty(userAgent))
                {
                    throw new Exception("User-Agent is incorrect!");
                }

                if (string.IsNullOrEmpty(websiteUrl) || !websiteUrl.Contains(".") || !websiteUrl.Contains("/") ||
                    !websiteUrl.Contains("http"))
                {
                    throw new Exception("Website URL is incorrect!");
                }

                if (string.IsNullOrEmpty(websiteKey))
                {
                    throw new Exception("Recaptcha Website Key is incorrect!");
                }

                var jObj = new JObject();

                jObj["softId"] = 2;
                jObj["clientKey"] = clientKey;
                jObj["task"] = new JObject
                {
                    ["type"] = type,
                    ["websiteURL"] = websiteUrl,
                    ["websiteKey"] = websiteKey,
                    ["websiteSToken"] = websiteSToken,
                    ["userAgent"] = userAgent
                };

                if (proxyType != null)
                {
                    jObj["task"]["proxyType"] = proxyType.ToString();
                    jObj["task"]["proxyAddress"] = proxyAddress;
                    jObj["task"]["proxyPort"] = proxyPort;
                    jObj["task"]["proxyLogin"] = proxyLogin;
                    jObj["task"]["proxyPassword"] = proxyPassword;
                }

                try
                {
                    var resultJson = JsonPostRequest(host, "createTask",
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));

                    int? taskId = null;
                    int? errorId = null;
                    string errorCode = null;
                    string errorDescription = null;

                    try
                    {
                        taskId = int.Parse(resultJson["taskId"].ToString());
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorId = int.Parse(resultJson["errorId"].ToString());
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorCode = resultJson["errorCode"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorDescription = resultJson["errorDescription"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    return new AnticaptchaTask(
                        taskId,
                        errorId,
                        errorCode,
                        errorDescription
                        );
                }
                catch
                {
                    // ignored
                }

                return null;
            }

            private static string ImagePathToBase64String(string path)
            {
                try
                {
                    using (var image = Image.FromFile(path))
                    {
                        using (var m = new MemoryStream())
                        {
                            image.Save(m, image.RawFormat);
                            var imageBytes = m.ToArray();

                            // Convert byte[] to Base64 String
                            var base64String = Convert.ToBase64String(imageBytes);

                            return base64String;
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }

            /// <summary>
            ///     Creates "image to text" task and sends it to anti-captcha.com
            /// </summary>
            /// <param name="host"></param>
            /// <param name="clientKey"></param>
            /// <param name="pathToImageOrBase64Body">You can set just a path to your image in the filesystem or a base64-encoded image</param>
            /// <param name="phrase"></param>
            /// <param name="_case"></param>
            /// <param name="numeric"></param>
            /// <param name="math"></param>
            /// <param name="minLength"></param>
            /// <param name="maxLength"></param>
            /// <returns>AnticaptchaTask with taskId or error information</returns>
            public static AnticaptchaTask CreateImageToTextTask(string host, string clientKey,
                string pathToImageOrBase64Body,
                bool? phrase = null, bool? _case = null, int? numeric = null,
                bool? math = null, int? minLength = null, int? maxLength = null)
            {
                try
                {
                    if (File.Exists(pathToImageOrBase64Body))
                    {
                        pathToImageOrBase64Body = ImagePathToBase64String(pathToImageOrBase64Body);
                    }
                }
                catch
                {
                    // ignored
                }

                var jObj = new JObject();

                jObj["softId"] = 2;
                jObj["clientKey"] = clientKey;
                jObj["task"] = new JObject();
                jObj["task"]["type"] = "ImageToTextTask";
                jObj["task"]["body"] = pathToImageOrBase64Body.Replace("\r", "").Replace("\n", "").Trim();

                if (phrase != null)
                {
                    jObj["task"]["phrase"] = phrase;
                }

                if (_case != null)
                {
                    jObj["task"]["case"] = _case;
                }

                if (numeric != null)
                {
                    jObj["task"]["numeric"] = numeric;
                }

                if (math != null)
                {
                    jObj["task"]["math"] = math;
                }

                if (minLength != null)
                {
                    jObj["task"]["minLength"] = minLength;
                }

                if (maxLength != null)
                {
                    jObj["task"]["maxLength"] = maxLength;
                }

                try
                {
                    var resultJson = JsonPostRequest(
                        host,
                        "createTask",
                        JsonConvert.SerializeObject(jObj, Formatting.Indented)
                        );

                    int? taskId = null;
                    int? errorId = null;
                    string errorCode = null;
                    string errorDescription = null;

                    try
                    {
                        taskId = int.Parse(resultJson["taskId"].ToString());
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorId = int.Parse(resultJson["errorId"].ToString());
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorCode = resultJson["errorCode"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorDescription = resultJson["errorDescription"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    return new AnticaptchaTask(
                        taskId,
                        errorId,
                        errorCode,
                        errorDescription
                        );
                }
                catch
                {
                    // ignored
                }

                return null;
            }

            public static AnticaptchaResult GetTaskResult(string host, string clientKey, AnticaptchaTask task)
            {
                var jObj = new JObject
                {
                    ["clientKey"] = clientKey,
                    ["taskId"] = task.GetTaskId()
                };


                try
                {
                    JObject resultJson = JsonPostRequest(host, "getTaskResult",
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));

                    var status = AnticaptchaResult.Status.unknown;

                    try
                    {
                        status = resultJson["status"].ToString().Equals("ready")
                            ? AnticaptchaResult.Status.ready
                            : AnticaptchaResult.Status.processing;
                    }
                    catch
                    {
                        // ignored
                    }

                    string solution;
                    int? errorId = null;
                    string errorCode = null;
                    string errorDescription = null;
                    double? cost = null;
                    string ip = null;
                    int? createTime = null;
                    int? endTime = null;
                    int? solveCount = null;

                    try
                    {
                        solution = resultJson["solution"]["gRecaptchaResponse"].ToString();
                    }
                    catch
                    {
                        try
                        {
                            solution = resultJson["solution"]["text"].ToString();
                        }
                        catch
                        {
                            solution = null;
                        }
                    }

                    try
                    {
                        errorId = resultJson["errorId"].ToObject<int>();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorCode = resultJson["errorCode"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        errorDescription = resultJson["errorDescription"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        cost = double.Parse(resultJson["cost"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        createTime = resultJson["createTime"].ToObject<int>();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        endTime = resultJson["endTime"].ToObject<int>();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        solveCount = resultJson["solveCount"].ToObject<int>();
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        ip = resultJson["ip"].ToString();
                    }
                    catch
                    {
                        // ignored
                    }

                    return new AnticaptchaResult(
                        status,
                        solution,
                        errorId,
                        errorCode,
                        errorDescription,
                        cost,
                        ip,
                        createTime,
                        endTime,
                        solveCount
                        );
                }
                catch
                {
                    // ignored
                }

                return null;
            }
        }

        #endregion

        #region Http Helper

        public class HttpHelper
        {
            public static JObject Post(Uri url, string post)
            {
                JObject result = null;
                var postBody = Encoding.UTF8.GetBytes(post);
                var request = (HttpWebRequest)WebRequest.Create(url);

                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = postBody.Length;

                try
                {
                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(postBody, 0, postBody.Length);
                        stream.Close();
                    }

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        var strreader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                        result = JObject.Parse(strreader.ReadToEnd());

                        response.Close();
                    }
                }
                catch
                {
                    return null;
                }

                return result;
            }
        }

        #endregion

    }


}
