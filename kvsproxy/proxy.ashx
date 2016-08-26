<%@ WebHandler Language="C#" Class="proxy" %>
/* В кодировке UTF-8 эту строку можно прочесть.
  This proxy page does not have any security checks. It is highly recommended
  that a user deploying this proxy page on their web server, add appropriate
  security checks, for example checking request path, username/password, target
  url, etc.
 *
 * url mapping
 * when serverItem url="http://localhost:8080/t/keyvalstor" and serverType="Zope"
 * then
 * http://vdesk.algis.com/kvsproxy/Proxy.ashx?get=foo
 * -> http://localhost:8080/t/keyvalstor/foo/getValue
 * and
 * http://vdesk.algis.com/kvsproxy/Proxy.ashx?id=foo&text=bar
 * -> http://localhost:8080/t/keyvalstor/manage_addProduct/VKeyValObj/addFunction?id=foo&text=bar
 *
 * when serverItem url="http://keyvalstor.algis.com:7379" and serverType="Webdis"
 * then
 * http://vdesk.algis.com/kvsproxy/Proxy.ashx?get=foo
 * -> http://keyvalstor.algis.com:7379/GET/foo.txt
 * and http://vdesk.algis.com/kvsproxy/Proxy.ashx?id=foo&text=bar/rab.arb
 * -> http://keyvalstor.algis.com:7379/SET/foo/bar%2Frab%2Earb
*/
using System;
//using System.Drawing;
using System.IO;
using System.Web;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Web.Caching;
using System.Net;

/// <summary>
/// Forwards requests to an ArcGIS Server REST resource. Uses information in
/// the proxy.config file to determine properties of the server.
/// </summary>
public class proxy : IHttpHandler
{
    ProxyConfig config;

    public proxy()
    {
        config = ProxyConfig.GetCurrentConfig();
    } // public proxy()


	public bool IsReusable {
		get {
			return false;
		}
	} // public bool IsReusable


	private string uri = "";
	private HttpContext context = null;
	private HttpResponse response = null;
	private string targeturi = "", data="", key = "";
	private System.Net.ICredentials credentials = null;
	private HttpWebRequest req = null;
	private string method = "";
	private string servuri = "";
	private string servtype = "";


    public void ProcessRequest(HttpContext cntxt)
    {
		this.context = cntxt;
        this.response = context.Response;

        // Get the URL requested by the client (take the entire querystring at once
        //  to handle the case of the URL itself containing querystring parameters)
        // string uri = Uri.UnescapeDataString(context.Request.QueryString.ToString());
		// for cyrillic urls
		if(context.Request.Url.Query.Length > 0)
			this.uri = context.Request.Url.Query.Substring(1);
		// Debug: Ping to make sure avaliability (eg. http://localhost/TestApp/proxy.ashx?ping)
		if(uri.StartsWith("ping")) {
			context.Response.ContentType = "text/plain";
			context.Response.Write("Hello proxy");
			return;
		}

		parseInput();
		if((method.Length == 0 || key.Length == 0) || (data.Length == 0 && method.Equals("POST")) ) {
			context.Response.ContentType = "text/plain";
			context.Response.Write(string.Format("Non format request: {0}; {1}", uri, data));
			return;
		}

		prepareToSend();
		if(targeturi.Length == 0) {
			context.Response.ContentType = "text/plain";
			context.Response.Write(string.Format("Wrong configuration: {0}; {1}; {2}", servuri, servtype, method));
			return;
		}
		//debug
		//context.Response.ContentType = "text/plain";
		//context.Response.Write(string.Format("key '{0}'; data '{1}'; meth '{2}'", key, data, method));
		//return;

		doSendAndReply();
    } // public void ProcessRequest(HttpContext context)


	private void doSendAndReply() {
		// Send the request to the server
		System.Net.WebResponse serverResponse = null;
		try {
			serverResponse = req.GetResponse();
		}
		catch(System.Net.WebException webExc) {
			response.StatusCode = 500;
			response.StatusDescription = webExc.Status.ToString();
			response.Write(webExc.Response);
			response.Write(string.Format("\nFailed request: {0}; {1}", uri, data));
			response.End();
			return;
		}

		// Set up the response to the client
		if(serverResponse != null) {
			response.ContentType = serverResponse.ContentType;
			using(Stream byteStream = serverResponse.GetResponseStream()) {
				byte[] outb = ReadFully(serverResponse.GetResponseStream(), 0);
				response.OutputStream.Write(outb, 0, outb.Length);
				serverResponse.Close();
			}
		}
		response.End();
	} // private void doSendAndReply()


	private void parseInput() {
		// format
		//	set uri (must be POST) id=foo&amp;text=bar
		//	get uri get=foo
		if(uri.StartsWith("get=") && context.Request.HttpMethod.Equals("GET")) {
			// if get value
			method = "GET";
			string[] parts = uri.Split(new string[] { "get=" }, StringSplitOptions.RemoveEmptyEntries);
			if(parts.Length > 0) key = parts[0];
		}
		else if(uri.Length == 0 && context.Request.HttpMethod.Equals("POST")) {
			// if set value
			method = "SET";
			getDataFromInpStream(); // raw data (id=foo&amp;text=bar)
			if(data.Length > 11 && data.StartsWith("id=")) { ; }
			else { // bad request
				return;
			}
			splitRawData(); // split data to (key,data)
		}
		else { // bad request
			method = "";
			return;
		}

		this.servuri = config.GetServerUrl(); // http://localhost:8080/t/keyvalstor || http://keyvalstor.algis.com:7379
		this.servtype = config.GetServerType(); // Zope || Webdis
		// todo: check servuri and servtype

		// if using Windows/HTTP authentication
		if(!string.IsNullOrEmpty(config.GetUser(uri))) {
			this.credentials = getCredentials(uri);
		}
	} // private void parseInput()


	private void getDataFromInpStream() {
		context.Request.InputStream.Position = 0;
		int len = (int)context.Request.InputStream.Length;
		byte[] buf = new byte[context.Request.InputStream.Length];
		context.Request.InputStream.Read(buf, 0, (int)context.Request.InputStream.Length);
		data = System.Text.Encoding.UTF8.GetString(buf, 0, (int)len);
	} // private void getDataFromInpStream()

	private void splitRawData() {
		string[] parts = data.Split(new string[] { "&amp;text=" }, StringSplitOptions.RemoveEmptyEntries);
		if(parts.Length > 1) {
			key = parts[0].Substring(3);
			data = parts[1];
		}
	} // private void splitRawData()


	private void prepareToSend() {
		// target uri for Zope server
		// set http://localhost:8080/t/keyvalstor/manage_addProduct/VKeyValObj/addFunction  POST id=foo&amp;text=bar
		// get http://localhost:8080/t/keyvalstor/foo/getValue
		// target uri for Webdis server
		// set http://keyvalstor.algis.com:7379/ POST SET/foo/bar (значение «bar/rab.arb» вебдису надо скормить как «bar%2Frab%2Earb»)
		// get http://keyvalstor.algis.com:7379/GET/foo.txt

		if(servtype.StartsWith("Zope")) {
			prepare4Zope();
		}
		else if(servtype.StartsWith("Webdis")) {
			prepare4Webdis();
		}
		else { // wrong server
			targeturi = "";
		}
		if(targeturi.Length == 0) return;

		this.req = (HttpWebRequest)System.Net.WebRequest.Create(new Uri(targeturi));
		req.AllowAutoRedirect = false;
		req.Method = context.Request.HttpMethod;

		// Assign credentials if using Windows/HTTP authentication
		if(credentials != null) {
			req.PreAuthenticate = true;
			req.Credentials = credentials;
		}

		// Set body of request for POST requests
		if(method.Equals("SET")) {
			var msData = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
			byte[] bytes = new byte[msData.Length];
			msData.Read(bytes, 0, (int)msData.Length);

			req.ContentLength = bytes.Length;
			req.ContentType = "application/x-www-form-urlencoded";
			using(Stream outputStream = req.GetRequestStream()) {
				outputStream.Write(bytes, 0, bytes.Length);
			}
		}
	} // private void prepareToSend()


	private void prepare4Zope() {
		if(method.Equals("GET")) {
			targeturi = string.Format("{0}/{1}/getValue", servuri, key);
		}
		else if(method.Equals("SET")) {
			targeturi = string.Format("{0}/manage_addProduct/VKeyValObj/addFunction", servuri);
			// prepare POST data (id=foo&amp;text=bar)
			data = string.Format("id={0}&amp;text={1}", key, data);
		}
		else { // wrong method
			targeturi = "";
		}
	} // private void prepare4Zope()


	private void prepare4Webdis() {
		if(method.Equals("GET")) {
			targeturi = string.Format("{0}/GET/{1}.txt", servuri, key);
		}
		else if(method.Equals("SET")) {
			targeturi = string.Format("{0}/", servuri);
			// prepare POST data (bar.replace('/', '%2F').replace('.', '%2E'); SET/foo/bar)
			// fubar: Webdis cut of text after '?'
			// todo: Would be better urlEncode all data string
			data = data.Replace("/", "%2F").Replace(".", "%2E").Replace("?", "%3F");
			data = string.Format("SET/{0}/{1}", key, data);
		}
		else { // wrong method
			targeturi = "";
		}
	} // private void prepare4Webdis()


    /// <summary>
    /// Reads data from a stream until the end is reached. The
    /// data is returned as a byte array. An IOException is
    /// thrown if any of the underlying IO calls fail.
    /// </summary>
    /// <param name="stream">The stream to read data from</param>
    /// <param name="initialLength">The initial buffer length</param>
    public static byte[] ReadFully(Stream stream, int initialLength)
    {
        // If we've been passed an unhelpful initial length, just
        // use 32K.
        if (initialLength < 1)
        {
            initialLength = 32768;
        }

        byte[] buffer = new byte[initialLength];
        int read = 0;

        int chunk;
        while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
        {
            read += chunk;

            // If we've reached the end of our buffer, check to see if there's
            // any more information
            if (read == buffer.Length)
            {
                int nextByte = stream.ReadByte();

                // End of stream? If so, we're done
                if (nextByte == -1)
                {
                    return buffer;
                }

                // Nope. Resize the buffer, put in the byte we've just
                // read, and continue
                byte[] newBuffer = new byte[buffer.Length * 2];
                Array.Copy(buffer, newBuffer, buffer.Length);
                newBuffer[read] = (byte)nextByte;
                buffer = newBuffer;
                read++;
            }
        }
        // Buffer is now too big. Shrink it.
        byte[] ret = new byte[read];
        Array.Copy(buffer, ret, read);
        return ret;
    } // public static byte[] ReadFully(Stream stream, int initialLength)


    // Gets the token for a server URL from a configuration file
    private string getTokenFromConfigFile(string uri)
    {
        try
        {
            if (config != null)
                return config.GetToken(uri);
            else
                throw new ApplicationException(
                    "Proxy.config file does not exist at application root, or is not readable.");
        }
        catch (InvalidOperationException)
        {
            // Proxy is being used for an unsupported service (proxy.config has mustMatch="true")
            HttpResponse response = HttpContext.Current.Response;
            response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
            response.End();
        }
        catch (Exception e)
        {
            if (e is ApplicationException)
                throw e;

            // just return an empty string at this point
            // -- may want to throw an exception, or add to a log file
        }

        return string.Empty;
    } // private string getTokenFromConfigFile(string uri)


    public System.Net.ICredentials getCredentials(string url)
    {
        try
        {
            if (config != null)
            {
                foreach (ServerItem si in config.ServerItems)
                    if (url.StartsWith(si.Url, true, null))
                    {
                        string url_401 = url.Substring(0, url.IndexOf("Server") + 6);

                        if (HttpRuntime.Cache[url_401] == null)
                        {
                            HttpWebRequest webRequest_401 = null;
                            webRequest_401 = (HttpWebRequest)HttpWebRequest.Create(url_401);
                            webRequest_401.ContentType = "text/xml;charset=\"utf-8\"";
                            webRequest_401.Method = "GET";
                            webRequest_401.Accept = "text/xml";

                            HttpWebResponse webResponse_401 = null;
                            while (webResponse_401 == null || webResponse_401.StatusCode != HttpStatusCode.OK)
                            {
                                try
                                {
                                    webResponse_401 = (System.Net.HttpWebResponse)webRequest_401.GetResponse();
                                }
                                catch (System.Net.WebException webex)
                                {
                                    System.Net.HttpWebResponse webexResponse = (HttpWebResponse)webex.Response;
                                    if (webexResponse.StatusCode == HttpStatusCode.Unauthorized)
                                    {
                                        if (webRequest_401.Credentials == null)
                                        {
                                            webRequest_401 = (HttpWebRequest)HttpWebRequest.Create(url_401);
                                            webRequest_401.Credentials =
                                                new System.Net.NetworkCredential(si.Username, si.Password, si.Domain);
                                        }
                                        else // if original credentials not accepted, throw exception
                                            throw webex;
                                    }
                                    else // if status code unrecognized, throw exception
                                        throw webex;
                                }
                                catch (Exception ex) { throw ex; }
                            }

                            if (webResponse_401 != null)
                                webResponse_401.Close();

                            HttpRuntime.Cache.Insert(url_401, webRequest_401.Credentials);
                        }
                        return (ICredentials)HttpRuntime.Cache[url_401];
                    }
            }
        }
        catch (InvalidOperationException)
        {
            // Proxy is being used for an unsupported service (proxy.config has mustMatch="true")
            HttpResponse response = HttpContext.Current.Response;
            response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
            response.End();
        }
        catch (Exception e)
        {
            if (e is ApplicationException)
                throw e;

            // just return an empty string at this point
            // -- may want to throw an exception, or add to a log file
        }

        return null;
    } // public System.Net.ICredentials getCredentials(string url)


    public string generateToken(string url)
    {
        try
        {
            if (config != null)
            {
                foreach (ServerItem si in config.ServerItems)
                    if (url.StartsWith(si.Url, true, null))
                    {
                        string theToken = null;

                        // If a token has been generated, check the expire time
                        if (HttpRuntime.Cache[si.Url] != null)
                        {
                            string existingToken = (HttpRuntime.Cache[si.Url] as
                            Dictionary<string, object>)["token"] as string;

                            DateTime expireTime = (DateTime)((HttpRuntime.Cache[si.Url] as
                            Dictionary<string, object>)["timeout"]);

                            // If token not expired, return it
                            if (DateTime.Now.CompareTo(expireTime) < 0)
                                theToken = existingToken;
                        }

                        // If token not available or expired, generate one and store it in cache
                        if (theToken == null)
                        {
                            string tokenServiceUrl = string.Format("{0}?request=getToken&username={1}&password={2}",
                                si.TokenUrl, si.Username, si.Password);

                            int timeout = 60;
                            if (si.Timeout > 0)
                                timeout = si.Timeout;

                            tokenServiceUrl += string.Format("&expiration={0}", timeout);
                            DateTime endTime = DateTime.Now.AddMinutes(timeout);

                            System.Net.WebRequest request = System.Net.WebRequest.Create(tokenServiceUrl);
                            System.Net.WebResponse response = request.GetResponse();
                            System.IO.Stream responseStream = response.GetResponseStream();
                            System.IO.StreamReader readStream = new System.IO.StreamReader(responseStream);
                            theToken = readStream.ReadToEnd();

                            Dictionary<string, object> serverItemEntries = new Dictionary<string, object>();
                            serverItemEntries.Add("token", theToken);
                            serverItemEntries.Add("timeout", endTime);

                            HttpRuntime.Cache.Insert(si.Url, serverItemEntries);
                        }
                        return theToken;
                    }
            }
        }
        catch (InvalidOperationException)
        {
            // Proxy is being used for an unsupported service (proxy.config has mustMatch="true")
            HttpResponse response = HttpContext.Current.Response;
            response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
            response.End();
        }
        catch (Exception e)
        {
            if (e is ApplicationException)
                throw e;

            // just return an empty string at this point
            // -- may want to throw an exception, or add to a log file
        }

        return string.Empty;
	} // public string generateToken(string url)
} // public class proxy : IHttpHandler


[XmlRoot("ProxyConfig")]
public class ProxyConfig
{
    #region Static Members

    private static object _lockobject = new object();

    public static ProxyConfig LoadProxyConfig(string fileName)
    {
        ProxyConfig config = null;

        lock (_lockobject)
        {
            if (System.IO.File.Exists(fileName))
            {
                XmlSerializer reader = new XmlSerializer(typeof(ProxyConfig));
                using (System.IO.StreamReader file = new System.IO.StreamReader(fileName))
                {
                    config = (ProxyConfig)reader.Deserialize(file);
                }
            }
        }

        return config;
    } // public static ProxyConfig LoadProxyConfig(string fileName)


    public static ProxyConfig GetCurrentConfig()
    {
        ProxyConfig config = HttpRuntime.Cache["proxyConfig"] as ProxyConfig;
        if (config == null)
        {
            string fileName = GetFilename(HttpContext.Current);
            config = LoadProxyConfig(fileName);

            if (config != null)
            {
                CacheDependency dep = new CacheDependency(fileName);
                HttpRuntime.Cache.Insert("proxyConfig", config, dep);
            }
        }

        return config;
    } // public static ProxyConfig GetCurrentConfig()


    // Location of the proxy.config file
    public static string GetFilename(HttpContext context)
    {
        return context.Server.MapPath("proxy.config");
    } // public static string GetFilename(HttpContext context)
	#endregion // Static Members


	ServerItem[] serverItems;
    bool mustMatch;

    [XmlArray("serverItems")]
    [XmlArrayItem("serverItem")]
    public ServerItem[] ServerItems
    {
        get { return this.serverItems; }
        set { this.serverItems = value; }
    }

    [XmlAttribute("mustMatch")]
    public bool MustMatch
    {
        get { return mustMatch; }
        set { mustMatch = value; }
    }


    public string GetToken(string uri)
    {
        foreach (ServerItem su in serverItems)
        {
            if (su.MatchAll && uri.StartsWith(su.Url, StringComparison.InvariantCultureIgnoreCase))
            {
                return su.Token;
            }
            else
            {
                if (String.Compare(uri, su.Url, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return su.Token;
            }
        }

        if (mustMatch)
            throw new InvalidOperationException();

        return string.Empty;
    } // public string GetToken(string uri)


    public string GetUser(string uri)
    {
        foreach (ServerItem su in serverItems)
        {
            if (su.MatchAll && uri.StartsWith(su.Url, StringComparison.InvariantCultureIgnoreCase))
            {
                return su.Username;
            }
            else
            {
                if (String.Compare(uri, su.Url, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return su.Username;
            }
        }

        if (mustMatch)
            throw new InvalidOperationException();

        return string.Empty;
	} // public string GetUser(string uri)


	public string GetServerUrl() {
		foreach(ServerItem su in serverItems) {
			if(su.Url.Length > 10) {
				return su.Url;
			}
		}
		return string.Empty;
	} // public string GetServerUrl()


	public string GetServerType() {
		foreach(ServerItem su in serverItems) {
			if(su.Url.Length > 10) {
				return su.serverType;
			}
		}
		return string.Empty;
	} // public string GetServerType()

} // public class ProxyConfig


public class ServerItem
{
    string url;
	string _serverType;
    bool matchAll;
    string token;
    string tokenUrl;
    string domain;
    string username;
    string password;
    int timeout;

    [XmlAttribute("url")]
    public string Url
    {
        get { return url; }
        set { url = value; }
    }

	[XmlAttribute("serverType")]
	public string serverType {
		get { return _serverType; }
		set { _serverType = value; }
	}

    [XmlAttribute("matchAll")]
    public bool MatchAll
    {
        get { return matchAll; }
        set { matchAll = value; }
    }

    [XmlAttribute("token")]
    public string Token
    {
        get { return token; }
        set { token = value; }
    }

    [XmlAttribute("tokenUrl")]
    public string TokenUrl
    {
        get { return tokenUrl; }
        set { tokenUrl = value; }
    }

    [XmlAttribute("domain")]
    public string Domain
    {
        get { return domain; }
        set { domain = value; }
    }

    [XmlAttribute("username")]
    public string Username
    {
        get { return username; }
        set { username = value; }
    }

    [XmlAttribute("password")]
    public string Password
    {
        get { return password; }
        set { password = value; }
    }

    [XmlAttribute("timeout")]
    public int Timeout
    {
        get { return timeout; }
        set { timeout = value; }
    }
} // public class ServerItem
