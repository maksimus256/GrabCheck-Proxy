using Leaf.xNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProxyChecker
{
    class Program
    {
        static List<Thread> threads = new List<Thread>();
        struct ProxyData
        {
            public string Proxy;
            public int Ping;
            public ProxyData(string Proxy, int Ping)
            {
                this.Proxy = Proxy;
                this.Ping = Ping;
            }
        }
        static List<string> proxy_parse = new List<string>();
        static List<ProxyData> ProxyList = new List<ProxyData>();
        static void Main(string[] args)
        {
            proxy_graber();
            proxy_check();
        }
        static public void proxy_graber()
        {
            
            List<string> Urls = new List<string>();
            Urls.Add("https://www.proxy-list.download/api/v1/get?type=socks5");
            Urls.Add("https://raw.githubusercontent.com/TheSpeedX/SOCKS-List/master/socks5.txt");
            Urls.Add("https://raw.githubusercontent.com/ShiftyTR/Proxy-List/master/socks5.txt");
            Urls.Add("https://raw.githubusercontent.com/jetkai/proxy-list/main/online-proxies/txt/proxies-socks5.txt");
            Urls.Add("https://api.proxyscrape.com/v2/?request=displayproxies&protocol=socks5");
            IEnumerator enumerator = null;
            int proxy_count = 0;
            var request = new HttpRequest();
            foreach (string Url in Urls)
            {
                try
                {

                    request.UserAgent = Http.ChromeUserAgent();
                    request.EnableMiddleHeaders = true;
                    request.IgnoreProtocolErrors = true;
                    var checkReq = request.Get(Url).ToString();
                    //string empty = client.DownloadString(new Uri(Url));
                    enumerator = (new Regex("(\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3})(?=[^\\d])\\s*:?\\s*(\\d{2,5})")).Matches(checkReq).GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        Match current = (Match)enumerator.Current;
                        proxy_count++;
                        if (!proxy_parse.Contains(current.Value))
                            proxy_parse.Add(current.Value);
                    }

                }
                catch (Exception exception)
                {
                }
            };
            Console.WriteLine("\nProxies all: " + proxy_count + " Unique proxies: " + proxy_parse.Count);
            using (TextWriter tw = new StreamWriter("proxies.txt"))
            {
              
                foreach (var s in proxy_parse)
                    tw.WriteLine(s);
            }
           // Console.Read();
        }
        static public void proxy_check()
        {
            int counter = 0;
            int total = 0;
            int good = 0;
            Console.WriteLine("Read proxies from proxies.txt file");
            List<string> goodProxies = new List<string>();
            FileStream fileStream = new FileStream(@"proxies.txt", FileMode.Open, FileAccess.Read);

            File.Delete(@"goodProxies.txt");
            int threads_max = 300;
            Console.WriteLine("Checking proxies at {0} threads", threads_max);
            StreamReader sr = new StreamReader(fileStream);

            DateTime date = DateTime.Now;

            while (!sr.EndOfStream)
            {

                while (threads.Count >= threads_max)
                {
                    Thread.Sleep(100);
                    CheckThread();
                }
                string temp = sr.ReadLine();
                if (temp.Length < 9)
                    continue;
                counter++;

                Thread Check = new Thread(() =>
                {
                    if (CheckProxy(temp))
                    {
                        good++;
              
                    }

                    Console.Write("\rTested: {0}, threads: {2}, for {1}", total, (DateTime.Now - date).ToString(), threads.Count);
                    total++;
                });
                Check.Start();
                threads.Add(Check);

            }


            while (threads.Count != 0)
            {
                Thread.Sleep(300);
                CheckThread();
            }
            ProxyList.Sort((s1, s2) => s1.Ping.CompareTo(s2.Ping));
            using (TextWriter tw = new StreamWriter("goodProxies_sorted.txt"))
            {
                foreach (var s in ProxyList)
                    tw.WriteLine(s.Proxy + "|" + s.Ping);
            }

            TimeSpan took = DateTime.Now - date;
            sr.Close();
            fileStream.Close();
            Console.WriteLine("\nDone. Good proxies: " + good + " in total of " + total + ", took " + (DateTime.Now - date).ToString());
            Console.Read();
        }
        static void CheckThread()
        {
            if (threads.Count != 0)
                for (int i = 0; i < threads.Count; i++)
                {
                    if (threads[i].ThreadState == ThreadState.Unstarted)
                        threads[i].Start();
                    else
                    if (threads[i].ThreadState != ThreadState.Running && threads[i].ThreadState != ThreadState.WaitSleepJoin)
                        threads.RemoveAt(i);
                }
        }

        static bool CheckProxy(string proxy)
        {
            string site = "http://www.google.ru";
            bool proxy_work = false;
            try
            {

                using (var request = new HttpRequest(site))
                {
                    request.UserAgent = Http.ChromeUserAgent();
                    request.Proxy = Socks5ProxyClient.Parse(proxy);
                    request.Proxy.ConnectTimeout = 6000;
                    request.ConnectTimeout = 6000;
                    request.EnableMiddleHeaders = true;
                    request.Proxy.AbsoluteUriInStartingLine = false;
                    request.IgnoreProtocolErrors = true;
                    var checkReq = request.Get("/").StatusCode;
                    if (checkReq == Leaf.xNet.HttpStatusCode.OK)
                        proxy_work = true;
                }
            }
            catch
            {
                using (var request = new HttpRequest(site))
                {
                    request.UserAgent = Http.ChromeUserAgent();
                    request.Proxy = HttpProxyClient.Parse(proxy);
                    request.Proxy.ConnectTimeout = 6000;
                    request.ConnectTimeout = 6000;
                    request.EnableMiddleHeaders = true;
                    request.Proxy.AbsoluteUriInStartingLine = false;
                    request.IgnoreProtocolErrors = true;
                    try
                    {
                        var checkReq = request.Get("/").StatusCode;
                        if (checkReq == Leaf.xNet.HttpStatusCode.OK)


                            proxy_work = true;
                    }
                    catch { };
                }
            }
            try
            {
             
                DateTime now2 = DateTime.Now;
             
                if (proxy_work)
                {
                    var ping = new Ping();
                    int timeout = 500;
                    var ip = proxy.Split(':')[0];
                    var reply = ping.Send(ip, timeout);
                    if (reply.Status == IPStatus.Success)
                    {
                        using (StreamWriter w = new StreamWriter("goodProxies.txt", true))
                        {
                            w.WriteLine(proxy + "|" + reply.RoundtripTime);
                        }
                        ProxyList.Add(new ProxyData(proxy, (int)reply.RoundtripTime));
                        return true;
                    }
                    else
                        return false;
                }
                else
                    return false;
              
            }

            catch { }
            return false;
        }
    }
}