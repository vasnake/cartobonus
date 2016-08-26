using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Interactivity;
using System.ComponentModel.Composition;
//using System.ComponentModel;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;

namespace mwb02.AddIns {
    [Export(typeof(Behavior<Map>))]
    [DisplayName("TestBehavior")]
	[Description("Пусто")]
	[Category("CGIS Tools")]
    public class MyBehavior: Behavior<Map>, ISupportsConfiguration {

        private MyConfigDialog configDialog = new MyConfigDialog();

        #region ISupportsConfiguration members

        public void Configure() {
            // When the dialog opens, it shows the information saved from the last configuration
            MapApplication.Current.ShowWindow("Configuration", configDialog);
        }

        public void LoadConfiguration(string configData) {
            // Initialize the behavior's configuration with the saved configuration data. 
            // The dialog's textbox is used to store the configuration.
            if(configData == null)
                configData = string.Empty;
            configDialog.InputTextBox.Text = configData;
        }

        public string SaveConfiguration() {
            // Save the information from the configuration dialog
            return configDialog.InputTextBox.Text;
        }

        #endregion

        #region Behavior Overrides
        protected override void OnAttached() {
            base.OnAttached();
            // Use ShowWindow instead of MessageBox.  There is a bug with
            // Firefox 3.6 that crashes Silverlight when using MessageBox.Show.
            MapApplication.Current.ShowWindow("My Behavior", new TextBlock() {
                Text = "The saved configuration string is: '" + configDialog.InputTextBox.Text + "'",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(30),
                MaxWidth = 480
            });
        }
        #endregion
    }
}
