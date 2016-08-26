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

namespace mwb02.AddIns
{
    public partial class SaveWnd : UserControl
    {
        public VSave vmsApp;
        public SaveWnd()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        { // button Save onclick
            try
            {
                this.vmsApp.log("SaveWnd.button1_Click...");
                this.vmsApp.saveMap(textBox1.Text.Trim());
            }
            catch (Exception ex) { 
                vmsApp.log("SaveWnd.button1_Click, error: " + ex.Message);
                HtmlPage.Window.Alert("Сохранение невозможно, произошел сбой:\n "+ex.Message);
            }
        }
    }
}
