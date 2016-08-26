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
    public partial class RestoreWnd : UserControl
    {
        public VRestore vmsApp;
        public RestoreWnd()
        {
            InitializeComponent();
            button1.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }


        private void button1_Click(object sender, RoutedEventArgs e)
        {
            //  button LoadMap onclick
            try
            {
                this.vmsApp.log("RestoreWnd.button1_Click...");
                string filename = this.listBox1.SelectedItem.ToString();
                vmsApp.log("RestoreWnd.button1_Click, file: "+filename);
                this.vmsApp.restoreMap(filename);
            }
            catch (Exception ex)
            {
                vmsApp.log("RestoreWnd.button1_Click, error: " + ex.Message);
                HtmlPage.Window.Alert("Загрузка невозможна, произошел сбой:\n " + ex.Message);
            }
        }


        private void onSavesListSelChanged(object sender, SelectionChangedEventArgs e)
        {
            // on list of saves selection changed
            try
            {
                this.vmsApp.log("onSavesListSelChanged...");
                if (e.AddedItems.Count <= 0)
                {
                    button1.IsEnabled = false;
                    btnDelete.IsEnabled = false;
                    this.vmsApp.log("onSavesListSelChanged, sel removed");
                    return;
                }
                button1.IsEnabled = true;
                btnDelete.IsEnabled = true;
                // we can restore from selected item
            }
            catch (Exception ex) { vmsApp.log("onSavesListSelChanged, error: " + ex.Message); }           
        }


        private void btnDelete_Click(object sender, RoutedEventArgs e) {
            //  button LoadMap onclick
            try {
                vmsApp.log("btnDelete_Click...");
                string filename = this.listBox1.SelectedItem.ToString();
                vmsApp.log("RestoreWnd.btnDelete_Click, file: " + filename);
                this.vmsApp.deleteMap(filename);
                listBox1.Items.Remove(listBox1.SelectedItem);
            }
            catch(Exception ex) {
                vmsApp.log("RestoreWnd.btnDelete_Click, error: " + ex.Message);
                HtmlPage.Window.Alert("Удаление невозможно, произошел сбой:\n " + ex.Message);
            }
        }
    }
}
