using System;
using System.Windows;
using System.Windows.Browser;

namespace Ice.AddIns
{
    public static class Lib
    {
        /// <summary>
        /// if you are using Firefox with the Firebug add-on or Internet Explorer 8: use the console.log mechanism
        /// </summary>
        // http://kodierer.blogspot.com/2009/05/silverlight-logging-extension-method.html
        public static void Log(this object obj)
        {
            HtmlWindow window = HtmlPage.Window;
            var isConsoleAvailable = (bool)window.Eval("typeof(console) != 'undefined' && typeof(console.log) != 'undefined'");
            if (isConsoleAvailable)
            {
                var console = (window.Eval("console.log") as ScriptObject);
                if (console != null)
                {
                    //console.InvokeSelf(obj);
                    DateTime dateTime = DateTime.Now;
                    string output = string.Format("{0} {1}", dateTime.ToString("mm:ss"), obj);
                    console.InvokeSelf(output);
                }
            }
        }
    }
}