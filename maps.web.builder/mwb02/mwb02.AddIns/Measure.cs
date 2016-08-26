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

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Linq;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Actions;
using ESRI.ArcGIS.Client.Symbols;

using System.IO.IsolatedStorage;
using System.Json;

namespace mwb02.AddIns
{
    [Export(typeof(ICommand))]
    [DisplayName("Measure tools")]
	[Description("Измерить расстояние, площадь")]
	[Category("CGIS Tools")]
    [DefaultIcon("/mwb02.AddIns;component/Images/measure.png")]
    public class VMeasure : ICommand, ISupportsConfiguration
    {
        private MyConfigDialog configDialog = new MyConfigDialog();
        private debug dbgDialog = new debug();

        public VMeasure(){
            dbgDialog.textBlock1.Text = "Measure log:\n";
        }    

        #region ISupportsConfiguration members

        public void Configure()
        {
            // When the dialog opens, it shows the information saved from the last configuration
            MapApplication.Current.ShowWindow("Configuration", configDialog);
        } // public void Configure()

        public void LoadConfiguration(string configData)
        {
            // Initialize the behavior's configuration with the saved configuration data. 
            // The dialog's textbox is used to store the configuration.
            configDialog.InputTextBox.Text = configData;
        } // public void LoadConfiguration(string configData)

        public string SaveConfiguration()
        {
            // Save the information from the configuration dialog
            return configDialog.InputTextBox.Text;
        } // public string SaveConfiguration()

        #endregion // #region ISupportsConfiguration members


        #region ICommand members

        public void Execute(object parameter)
        {
            //MapApplication.Current.ShowWindow("Debug messages", dbgDialog, false);
            log("VMeasure.Execute, ...");

            // MyDrawSurface.DrawMode = DrawMode.None; //stops selection tools from working
            //var fs = new ESRI.ArcGIS.Client.Symbols.SimpleFillSymbol();
            //fs.Fill = null;
            //fs.BorderBrush = null;

            // http://forums.arcgis.com/threads/3696-Measure-Action-in-Code
            var m = new VMeasureAction() {
                AreaUnit = AreaUnit.SquareKilometers,
                MeasureMode = MeasureAction.Mode.Polygon,
                DisplayTotals = true,
                DistanceUnit = DistanceUnit.Kilometers,
                FillSymbol = configDialog.LayoutRoot.Resources["DefaultFillSymbol"] as FillSymbol
            };            
            m.Attach(MapApplication.Current.Map);
            m.Execute();
        } // public void Execute(object parameter)


        public bool CanExecute(object parameter)
        {
            return MapApplication.Current.Map != null;
        } // public bool CanExecute(object parameter)

        public event EventHandler CanExecuteChanged;

        #endregion // #region ICommand members

        public void log(String txt)
        {
            TextBox d = this.dbgDialog.textBlock1;
            d.Text += txt + "\n";
            System.Diagnostics.Debug.WriteLine(txt);
        }
    } // public class VMeasure : ICommand, ISupportsConfiguration


    public class VMeasureAction : ESRI.ArcGIS.Client.Actions.MeasureAction
    {
        public void Execute()
        {
            Invoke(null);
        }
    }
}
