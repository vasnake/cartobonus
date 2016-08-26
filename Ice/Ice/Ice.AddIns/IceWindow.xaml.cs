using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;

namespace Ice.AddIns
{
    public partial class IceWindow : UserControl
    {
        public IceWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

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

        /// <summary>
        /// Set the time intervals
        /// </summary>
        internal void SetTimer()
        {
            TimeExtent timeExtent = null;
			if(Layer is FeatureLayer) {
				timeExtent = ((FeatureLayer)Layer).TimeExtent;
			}
			else if(Layer is ArcGISDynamicMapServiceLayer) {
				timeExtent = ((ArcGISDynamicMapServiceLayer)Layer).TimeExtent;
			}
			else
				return;

            List<DateTime> intervals = new List<DateTime>();

            intervals.Add(timeExtent.Start);

            // irregular
            string[] dates = {
                "01/09/2008","01/16/2008","01/23/2008","01/30/2008","02/06/2008","02/13/2008","02/20/2008","03/19/2008","03/26/2008","04/02/2008","04/09/2008","04/16/2008","04/24/2008","04/29/2008","05/07/2008","05/14/2008","05/21/2008","05/28/2008","06/04/2008","06/10/2008","06/18/2008","06/25/2008","07/02/2008","07/10/2008","07/16/2008","07/23/2008","07/30/2008","08/07/2008","08/14/2008","08/21/2008","08/27/2008","09/04/2008","09/10/2008","09/17/2008","09/24/2008","10/01/2008","10/08/2008","10/15/2008","10/22/2008","10/29/2008","11/05/2008","11/12/2008","11/19/2008","11/26/2008","12/03/2008","12/10/2008","12/17/2008","12/24/2008",
                "12/31/2008","01/07/2009","01/14/2009","01/21/2009","01/28/2009","02/04/2009","02/11/2009","02/19/2009","02/25/2009","03/04/2009","03/11/2009","03/18/2009","03/25/2009","04/02/2009","04/08/2009","04/15/2009","04/22/2009","04/29/2009","05/06/2009","05/13/2009","05/20/2009","05/27/2009","06/03/2009","06/10/2009","06/17/2009","06/24/2009","07/01/2009","07/08/2009","07/15/2009","07/21/2009","07/28/2009","08/05/2009","08/11/2009","08/25/2009","09/01/2009","09/08/2009","09/15/2009","09/22/2009","09/29/2009","10/06/2009","10/13/2009","10/20/2009","10/27/2009","11/03/2009","11/11/2009","11/17/2009","12/01/2009","12/08/2009","12/15/2009","12/22/2009","12/29/2009",
                "01/05/2010","01/12/2010","01/19/2010","01/26/2010","02/02/2010","02/09/2010","02/16/2010","03/02/2010","03/09/2010","03/16/2010","03/23/2010","03/30/2010","04/06/2010","04/13/2010","04/20/2010","04/27/2010","05/04/2010","05/11/2010","05/18/2010","05/25/2010","06/01/2010","06/08/2010","06/15/2010","06/22/2010","06/29/2010","07/06/2010","07/13/2010","07/20/2010","07/27/2010","08/03/2010","08/10/2010","08/17/2010","08/24/2010","08/31/2010","09/07/2010","09/14/2010","09/21/2010","09/28/2010","10/05/2010","10/12/2010","10/19/2010","10/26/2010","11/02/2010","11/09/2010","11/16/2010","11/23/2010","11/30/2010","12/07/2010","12/14/2010","12/21/2010","12/28/2010"
            };
            foreach (string date in dates)
            {
                DateTime irr_interval = DateTime.Parse(date + " 00:00:00 GMT", CultureInfo.InvariantCulture);
                if (irr_interval > timeExtent.Start)
                    intervals.Add(irr_interval);
            }

            // regular
            DateTime reg_interval = DateTime.Parse("01/04/2011 00:00:00 GMT", CultureInfo.InvariantCulture);
            while (reg_interval < timeExtent.End)
            {
                if (reg_interval > timeExtent.Start)
                    intervals.Add(reg_interval);
                reg_interval = reg_interval.AddDays(7);
            }

            intervals.Add(timeExtent.End);

            /*
            // without irregular
            TimeSpan timeSpan = timeExtent.End.Subtract(timeExtent.Start);
            double timeInterval = Math.Floor(timeSpan.TotalDays/7);
            DateTime dt = timeExtent.Start;
            for (int i = 0; i < timeInterval ; i++)
            {
                intervals.Add(dt);
                dt = dt.AddDays(7);
            }
            */
            
            IceTimeSlider.MinimumValue = timeExtent.Start;
            IceTimeSlider.MaximumValue = timeExtent.End;
            IceTimeSlider.Intervals = intervals;

            Map.TimeExtent = new TimeExtent(timeExtent.Start, timeExtent.Start.AddDays(7));
        }
    }
}
