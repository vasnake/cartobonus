using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Browser;

namespace resizablewindow.test
{
    public partial class MainPage : UserControl
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            //throw new Exception("oops");
/*
 * it's work
            var fw = new System.Windows.Controls.FloatableWindow();
            fw.ParentLayoutRoot = LayoutRoot;
            fw.Title = "test окошко";
            fw.Height = 300; fw.Width = 333;
            fw.Content = "контент?";
            fw.Show(); // fw.ShowDialog();
*/
            testWindow1.Visibility = System.Windows.Visibility.Visible;
        }

        private void btnTest1_Click(object sender, RoutedEventArgs e)
        {
            //throw new Exception("btnTest1_Click not yet");
            "btnTest1_Click not yet".log();
        }

        private void testWindow1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(testWindow1.Height <= 50 || testWindow1.Width <= 50) {
                testWindow1.Height = 150;
                testWindow1.Width = 200;
            }
            testWindow1.Visibility = System.Windows.Visibility.Collapsed;
        }        
    }


    public static class VExtClass
    { // http://kodierer.blogspot.com/2009/05/silverlight-logging-extension-method.html
        /// <summary>
        /// if you are using Firefox with the Firebug add-on or
        /// Internet Explorer 8: Use the console.log mechanism
        /// </summary>
        /// <param name="obj"></param>
        public static void log(this object obj)
        {
            try {
                HtmlWindow window = HtmlPage.Window;
                var console = (window.Eval("console.log") as ScriptObject);
                DateTime dt = DateTime.Now;
                var txt = string.Format("{0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), obj);
                console.InvokeSelf(txt);
            }
            catch (Exception ex) {
                var msg = ex.Message;
                //MessageBox.Show(msg);
            }
        } // public static void Log(this object obj)
    }
}
