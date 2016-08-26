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
using System.ComponentModel;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Toolkit;

namespace Ice.AddIns
{
    [Export(typeof(Behavior<Map>))]
    [DisplayName("Ice Behavior")]
    public class IceBehavior : Behavior<Map>, ISupportsConfiguration
    {
        private MyConfigDialog configDialog = new MyConfigDialog();

        public IceWindow IceWindow;

        // Surface the selected layer as an instance property to make it bindable in xaml
        public Layer Layer
        {
            get { return MapApplication.Current.SelectedLayer; }
        }

        // Surface the map as an instance property to make it bindable in xaml
        public Map Map
        {
            get { return MapApplication.Current.Map; }
        }

        #region ISupportsConfiguration members

        public void Configure()
        {
            // When the dialog opens, it shows the information saved from the last configuration
            MapApplication.Current.ShowWindow("Configuration", configDialog);
        }

        public void LoadConfiguration(string configData)
        {
            // Initialize the behavior's configuration with the saved configuration data. 
            // The dialog's textbox is used to store the configuration.
            if (configData == null)
                configData = string.Empty;
            configDialog.InputTextBox.Text = configData;
        }

        public string SaveConfiguration()
        {
            // Save the information from the configuration dialog
            return configDialog.InputTextBox.Text;
        }

        #endregion

        #region Behavior Overrides
        protected override void OnAttached()
        {
            base.OnAttached();
            MapApplication.Current.SelectedLayerChanged += SelectedLayerChanged;
        }
        #endregion

        #region functions

        private void SelectedLayerChanged(object sender, EventArgs args)
        {
			string confUrl = configDialog.InputTextBox.Text.ToLower();
            if (Layer is ArcGISDynamicMapServiceLayer
            && ((ArcGISDynamicMapServiceLayer)Layer).TimeExtent != null)
            {
				var lyr = (ArcGISDynamicMapServiceLayer)Layer;
				if(!lyr.Url.ToLower().Replace("//", "/").Contains(confUrl.Replace("//", "/"))) {
					return;
				}
                //if (((ArcGISDynamicMapServiceLayer)Layer).Url == configDialog.InputTextBox.Text)
                //{
                    if (IceWindow == null)
                    {
                        //MapApplication.Current.ShowWindow("Ледовая обстановка", IceWindow, false);
                        IceWindow = MapApplication.Current.FindObjectInLayout("IceWin") as IceWindow;

                        IceWindow.DataContext = this;
                        IceWindow.SetTimer();
                    }

                    IceWindow.LayerName.Text = Layer.GetValue(MapApplication.LayerNameProperty) as string + ": ";
                    (MapApplication.Current.FindObjectInLayout("TimeSlider") as Border).Visibility = Visibility.Visible;
                //}
            }
            else
            {
                //MapApplication.Current.HideWindow(IceWindow);
                (MapApplication.Current.FindObjectInLayout("TimeSlider") as Border).Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
}
