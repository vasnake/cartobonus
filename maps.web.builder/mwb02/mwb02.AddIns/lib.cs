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
using System.Windows.Browser;

using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Json;
using System.Xml.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Actions;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Client.Toolkit;
using ESRI.ArcGIS.Client.FeatureService;

//using System.Reflection;

namespace mwb02.AddIns {

	public class VLayer {
		// Layer derivatives helper, make some abstraction

		public ESRI.ArcGIS.Client.Layer lyr = null;
		public string lyrName = "",
			lyrUrl = "undefined url",  // service url for layer or content for graphicslayer
			lyrType = "undefined type";
		public List<mwb02.AddIns.VRelationInfo> relations = null;

		public string ID { get { return lyr.ID; } }
		public bool Visible { get { return lyr.Visible; } }
		public double Opacity { get { return lyr.Opacity; } }
		private double _opacity = 255.0;
		public string proxy = "";
		public bool selected = false;
		public bool popupOn = false; // on/off popups
		public string identifyLayerIds = ""; // sublayers id's with popups enabled
		public string renderer = ""; // GraphicLayer renderer json string
		public ArcGISDynamicMapServiceLayer.RestImageFormat imageFormat = ArcGISDynamicMapServiceLayer.RestImageFormat.PNG24;

		private string oidFieldName = ""; // FeatureLayer OID field name

		public VLayer() {
			lyr = null;
		}

		public VLayer(Layer l) {
			lyr = l;
			getLyrSignature(null, l);
			if(MapApplication.Current.SelectedLayer != null &&
				MapApplication.Current.SelectedLayer.ID == lyr.ID) this.selected = true;
		}


		public VLayer(VLayerDescription ld) {
			lyr = null;
			lyrName = ld.name;
			lyrUrl = ld.url;
			lyrType = ld.type;
			selected = false;
			proxy = ld.proxy;
			if(ld.imageFormat == "PNG32") imageFormat = ArcGISDynamicMapServiceLayer.RestImageFormat.PNG32;

			helpCreateLayer(ld.id, true);
		} // public VLayer(VLayerDescription ld)


		public VLayer(JsonObject js) { //var vLyr = new VLayer(jsLyr);
			lyr = null;
			lyrName = js["name"];
			lyrUrl = js["url"];
			lyrType = js["type"];
			proxy = getFromJson(js, "proxy");
			selected = getBoolFromJson(js, "selected");
			popupOn = getBoolFromJson(js, "popupEnabled");
			identifyLayerIds = getFromJson(js, "identifyLayerIds");
			renderer = getFromJson(js, "renderer");

			try {
				_opacity = getFromJson(js, "opacity");
			}
			catch(Exception ex) { string.Format("VLayer(JsonObject) can't find 'opacity': {0}", ex.Message).clog(); }

			try {
				imageFormat = (ArcGISDynamicMapServiceLayer.RestImageFormat)(int)getFromJson(js, "ImageFormat");
			}
			catch(Exception ex) { string.Format("VLayer(JsonObject) can't find 'ImageFormat': {0}", ex.Message).clog(); }

			helpCreateLayer(js["id"], js["visible"]);
		} // public VLayer(JsonObject js)


		public JsonObject toJson() {
			var obj = new JsonObject {
                    {"name", lyrName},
                    {"url", lyrUrl},
                    {"type", lyrType},
                    {"proxy", proxy},
                    {"id", ID},
                    {"visible", Visible},
                    {"selected", selected},
					{"popupEnabled", popupOn},
					{"identifyLayerIds", identifyLayerIds},
					{"renderer", renderer},
					{"opacity", Opacity},
					{"ImageFormat", (int)imageFormat}
                };
			return obj;
		} // public JsonObject toJson()


		/// <summary>
		/// Create new Layer instance from this.lyr
		/// >>> foreach(var lyr in MapApplication.Current.Map.Layers) {
		/// >>>     var nl = new VLayer(lyr);
		/// >>>     nl.cloneLayer();
		/// >>>     frmPrint.Map.Layers.Add(nl.lyr);
		/// </summary>
		public void cloneLayer() {
			helpCreateLayer(lyr.ID, lyr.Visible);
		} // public void cloneLayer()


		private void helpCreateLayer(string id, bool vis) {
			lyr = createLayer(id, vis);
			if(lyr == null) {
				throw new Exception(string.Format(
					"VLayer.helpCreateLayer, can't create layer [{0}, {1}]", id, lyrUrl));
			}
			lyr.Visible = vis;
			lyr.ID = id;
			lyr.Opacity = _opacity;
		} // private void helpCreateLayer(string id, bool vis)


		public JsonValue getFromJson(JsonObject js, string key) {
			// small helper
			if(js.ContainsKey(key)) return js[key];
			return "";
		} // public JsonValue getFromJson(JsonObject js, string key)

		public bool getBoolFromJson(JsonObject js, string key) {
			var jv = getFromJson(js, key);
			if(jv.ToString().Trim().ToLower().Equals("true")) return true;
			return false;
		} // public bool getBoolFromJson(JsonObject js, string key)


		/// <summary>
		/// Return FeatureLayer or null from ArcGISDynamicMapServiceLayer by lyrID
		/// </summary>
		/// <param name="lyrID">ArcGISDynamicMapServiceLayer sublayer id</param>
		/// <returns>VLayer with FeatureLayer inside</returns>
		public VLayer getSubLayer(int lyrID) {
			if(this.lyrType == "ArcGISDynamicMapServiceLayer") {
				var ld = new mwb02.AddIns.VLayerDescription();
				ld.type = "FeatureLayer";
				ld.url = string.Format("{0}/{1}", this.lyrUrl, lyrID);
				ld.proxy = this.proxy;
				var res = new mwb02.AddIns.VLayer(ld);
				return res;
			}
			else { return null; }
		} // private Layer getSubLayer(mwb02.AddIns.VLayer lyr, int lyrID)


		/// <summary>
		/// oidFieldname = vfl.getOIDFieldnameOrAlias();
		/// vfl.lyr must be FeatureLayer and Initialized first.
		/// todo: rename to getEffectiveOIDFieldName, code must verify fieldname with feature.Attributes[fn]
		/// </summary>
		/// <returns>field alias if exists, fieldname otherwise</returns>
		public string getOIDFieldnameOrAlias() {
			if(this.oidFieldName.Length > 0) return this.oidFieldName;

			var fl = this.lyr as FeatureLayer;
			var fn = fl.LayerInfo.ObjectIdField;
			string fa = this.getFieldAlias(fn);
			if(fa != "") fn = fa; // because of bug in Graphic.Attributes[fieldname];
			return fn;
		} // public string getOIDFieldnameOrAlias()


		/// <summary>
		/// int objIdValue = vfl.getOID(feature);
		/// vfl.lyr must be FeatureLayer and Initialized first
		/// </summary>
		/// <param name="feature"></param>
		/// <returns>-1 if error</returns>
		public int getOID(ESRI.ArcGIS.Client.Graphic feature) {
			int res = -1;
			var fl = this.lyr as FeatureLayer;
			var fn = fl.LayerInfo.ObjectIdField;

			var val = string.Format("{0}", feature.Attributes[fn]);
			if(val.Length <= 0) {
				fn = this.getFieldAlias(fn);
				val = string.Format("{0}", feature.Attributes[fn]);
			}

			try {
				res = Int32.Parse(string.Format("{0}", val));
				this.oidFieldName = fn;
			}
			catch(Exception ex) {
				string.Format("VLayer.getOID fail, lyr {0}, fieldname '{1}', value '{2}', err {3}\n{4}",
					this.lyrUrl, fn, val, ex.Message, ex.StackTrace).clog();
			}
			return res;
		} // public int getOID(ESRI.ArcGIS.Client.Graphic feature)


		public FeatureLayer getFL() {
			if(this.lyrType != "FeatureLayer") {
				return null;
			}
			return this.lyr as FeatureLayer;
		} // public FeatureLayer getFL()


		public string getFieldAlias(string fieldname) {
			if(this.lyrType != "FeatureLayer") {
				throw new Exception("VLayer.getFieldAlias, layer must be FeatureLayer");
			}
			var fl = this.lyr as FeatureLayer;
			if(fl.LayerInfo == null) {
				throw new Exception("VLayer.getFieldAlias, call lyr.Initialize() first");
			}

			var fields = fl.LayerInfo.Fields;
			foreach(var f in fields) {
				if(f.Name == fieldname) {
					return f.Alias;
				}
			}
			return "";
		} // public string getFieldAlias(string fieldname)


		public VRelationInfo getRelation(ESRI.ArcGIS.Client.FeatureService.Relationship rel) {
			if(this.relations == null) return null;
			foreach(var ri in this.relations) {
				if(ri.id == rel.Id && ri.name == rel.Name && ri.tableId == rel.RelatedTableId)
					return ri;
			}
			return null;
		} // public VRelationInfo getRelation(ESRI.ArcGIS.Client.FeatureService.Relationship rel)


		public void initRelations() {
			if(this.lyrType == "FeatureLayer") {
				var fl = this.lyr as FeatureLayer;
				if(fl != null && fl.LayerInfo != null) {
					if(this.relations == null) this.relations = new List<VRelationInfo>();
					foreach(var rel in fl.LayerInfo.Relationships) {
						var relinfo = this.getRelation(rel);
						if(relinfo == null) {
							relinfo = new VRelationInfo(rel);
							this.relations.Add(relinfo);
						}
					}
				}
			}
		} // public void initRelations()


		private Layer createLayer(string id, bool vis) {
			// create Layer according to its Type
			string typ = lyrType;
			ESRI.ArcGIS.Client.Layer res = new ESRI.ArcGIS.Client.GraphicsLayer();

			if(typ == "ArcGISTiledMapServiceLayer") {
				var lr = new ESRI.ArcGIS.Client.ArcGISTiledMapServiceLayer();
				lr.Url = lyrUrl;
				lr.ProxyURL = proxy;
				res = lr;
			}
			else if(typ == "OpenStreetMapLayer") {
				var lr = new ESRI.ArcGIS.Client.Toolkit.DataSources.OpenStreetMapLayer();
				res = lr;
			}
			else if(typ == "ArcGISDynamicMapServiceLayer") {
				var lr = new ESRI.ArcGIS.Client.ArcGISDynamicMapServiceLayer();
				lr.Url = lyrUrl;
				lr.ProxyURL = proxy;
				lr.ImageFormat = imageFormat;
				res = lr;
			}
			else if(typ == "FeatureLayer") {
				var lr = new ESRI.ArcGIS.Client.FeatureLayer() { Url = lyrUrl, ProxyUrl = proxy };
				lr.OutFields.Add("*");
				lr.Mode = FeatureLayer.QueryMode.OnDemand;
				lr.Initialize(); // retrieve attribs from server
				var rr = rendererFromJson(renderer);
				if(rr != null) lr.Renderer = rr;
				res = lr;
			}
			else if(typ == "GraphicsLayer") {
				var gl = setContent(id, lyrUrl);
				var rr = rendererFromJson(renderer);
				if(rr != null) gl.Renderer = rr;
				res = gl;
			}

			if(res != null) {
				ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsPopupEnabled(res, popupOn);

				// sublayers popups on/off
				if(identifyLayerIds.Length <= 3) { ; }
				else {
					var xmlszn = new System.Xml.Serialization.XmlSerializer(typeof(System.Collections.ObjectModel.Collection<int>));
					var sr = new StringReader(identifyLayerIds);
					var ids = xmlszn.Deserialize(sr) as System.Collections.ObjectModel.Collection<int>;
					ESRI.ArcGIS.Mapping.Core.LayerExtensions.SetIdentifyLayerIds(res, ids);
				}
			}

			return res;
		} // private Layer createLayer(string id, bool vis)


		private void getLyrSignature(Map map, ESRI.ArcGIS.Client.Layer l) {
			// get all Layer parameters
			string typ = lyr.GetType().ToString();
			string[] parts = typ.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
			if(parts.Length > 0) typ = parts[parts.Length - 1];

			lyrType = typ;
			lyrName = MapApplication.GetLayerName(l);
			popupOn = ESRI.ArcGIS.Client.Extensibility.LayerProperties.GetIsPopupEnabled(l);

			// sublayers popups on/off http://forums.arcgis.com/threads/58106-Popup-for-visible-layers-only?highlight=popups
			var ids = ESRI.ArcGIS.Mapping.Core.LayerExtensions.GetIdentifyLayerIds(l);
			//var ids = new System.Collections.ObjectModel.Collection<int>();
			var xmlszn = new System.Xml.Serialization.XmlSerializer(typeof(System.Collections.ObjectModel.Collection<int>));
			var sw = new StringWriter();
			xmlszn.Serialize(sw, ids);
			identifyLayerIds = string.Format("{0}", sw.ToString().Trim());

			if(typ == "ArcGISTiledMapServiceLayer") {
				var lr = (ArcGISTiledMapServiceLayer)lyr;
				lyrUrl = lr.Url;
				proxy = lr.ProxyURL;
			}
			else if(typ == "OpenStreetMapLayer") {
				var lr = lyr as ESRI.ArcGIS.Client.Toolkit.DataSources.OpenStreetMapLayer;
				lyrUrl = "http://www.openstreetmap.org/";
			}
			else if(typ == "ArcGISDynamicMapServiceLayer") {
				var lr = (ArcGISDynamicMapServiceLayer)lyr;
				lyrUrl = lr.Url;
				proxy = lr.ProxyURL;
				imageFormat = lr.ImageFormat;
			}
			else if(typ == "FeatureLayer") {
				var lr = (FeatureLayer)lyr;
				lyrUrl = lr.Url;
				renderer = getRenderer(lr);
				proxy = lr.ProxyUrl;
			}
			else if(typ == "GraphicsLayer") {
				var lr = (GraphicsLayer)lyr;
				lyrUrl = getContent(lr);
				renderer = getRenderer(lr);
				proxy = "";
			}
			return;
		} // private string getLyrSignature(Map map, ESRI.ArcGIS.Client.Layer lyr)


		public static string getRenderer(GraphicsLayer gl) {
			// todo: LOG: Graphic Symbol is not serializable to JSON
			string res = "";
			var currCulture = System.Globalization.CultureInfo.CurrentCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

			try {
				var sr = gl.Renderer as SimpleRenderer;
				if(sr != null) res = sr.ToJson();
				else {
					var cbr = gl.Renderer as ClassBreaksRenderer;
					if(cbr != null) res = cbr.ToJson();
					else {
						var uvr = gl.Renderer as UniqueValueRenderer;
						if(uvr != null) res = uvr.ToJson();
					}
				}
			}
			catch(Exception ex) {
				string.Format("VLayer.getRenderer error: '{0}', lyr ID: '{1}'", ex.Message, gl.ID).clog();
			}

			System.Threading.Thread.CurrentThread.CurrentCulture = currCulture;
			return res;
		} // public static string getRenderer(GraphicsLayer gl) {


		public static IRenderer rendererFromJson(string json) {
			try {
				if(json != "") return Renderer.FromJson(json);
			}
			catch(Exception ex) {
				string.Format("VLayer.rendererFromJson, error: '{0}', json: '{1}'", ex.Message, json).clog();
			}
			return null;
		} // public static IRenderer rendererFromJson(string json)


		public static string getContent(GraphicsLayer gl) {
			// serialize GraphicsLayer
			var sb = new System.Text.StringBuilder();
			var xw = XmlWriter.Create(sb);
			gl.SerializeGraphics(xw);
			xw.Close();
			return sb.ToString();
		} // public string getContent()


		private ESRI.ArcGIS.Client.GraphicsLayer setContent(string id, string xmlContent) {
			// create and deserialize GraphicsLayer
			string.Format("VLayer.setContent, create new GraphicsLayer id '{0}'", id).clog();
			var gl = makeRLLayer(null, id, "");
			gl = setContent(gl, xmlContent);
			return gl;
		} // private ESRI.ArcGIS.Client.GraphicsLayer setContent(string id, string xmlContent)


		public static ESRI.ArcGIS.Client.GraphicsLayer setContent(
			ESRI.ArcGIS.Client.GraphicsLayer gl, string xmlContent) {
			var sr = new StringReader(xmlContent);
			var xr = XmlReader.Create(sr);
			//gl.Graphics.Clear();
			gl.DeserializeGraphics(xr);
			xr.Close();
			sr.Close();
			return gl;
		} // public static ESRI.ArcGIS.Client.GraphicsLayer setContent(ESRI.ArcGIS.Client.GraphicsLayer gl, string xmlContent)


		public string getAGSMapServiceUrl() {
			var res = "";
			if(lyrType == "ArcGISDynamicMapServiceLayer" ||
				lyrType == "ArcGISTiledMapServiceLayer") {
				res = lyrUrl;
			}
			else {
				if(lyrType == "FeatureLayer") {
					var pos = lyrUrl.LastIndexOf("/");
					res = lyrUrl.Substring(0, pos);
				}
			}
			return res;
		} // public string getAGSMapServiceUrl()


		/// <summary>
		/// Create and add to map a simple graphics layer, or return existed GL
		/// </summary>
		/// <returns>redline layer</returns>
		public static GraphicsLayer makeRLLayer(Map map, string layerID, string layerName) {
			if(map != null) {
				var graphicsLayer = map.Layers[layerID] as GraphicsLayer;
				if(graphicsLayer != null) {
					string.Format("VLayer.makeRLLayer, layer already exists, id '{0}'", layerID).clog();
					return graphicsLayer;
				}
			}

			string.Format("VLayer.makeRLLayer, create new GraphicsLayer, id '{0}'", layerID).clog();
			// don't react on hover
			var symbPMS = new PictureMarkerSymbol() {
				Height = 28,
				Width = 28,
				OffsetX = 14,
				OffsetY = 28,
				Source = new System.Windows.Media.Imaging.BitmapImage(
					new Uri("/Images/MarkerSymbols/Basic/RedTag.png", UriKind.RelativeOrAbsolute))
			};
			// react on hover
			var symbIFS = new ESRI.ArcGIS.Mapping.Core.Symbols.ImageFillSymbol() {
				Source = "/Images/MarkerSymbols/Basic/RedTag.png",
				//OffsetX = 14, OffsetY = 28,
				Size = 28,
				OriginX = 0.5, OriginY = 1
			};

			var gl = new GraphicsLayer() {
				ID = layerID,
				Renderer = new SimpleRenderer() {
					Symbol = symbIFS // new SimpleMarkerSymbol()
				},
				RendererTakesPrecedence = false
			};

			//ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsPopupEnabled(gl, false);

			// Set layer name in Map Contents
			//gl.SetValue(MapApplication.LayerNameProperty, lyrName);
			//* Remove the line that says "wmsLayer.SetValue(MapApplication.LayerNameProperty, layerName);" and replace it with "MapApplication.SetLayerName(wmsLayer, layerName);"
			//* http://forums.arcgis.com/threads/51206-Adding-WMS-Service?p=178500&viewfull=1#post178500
			//MapApplication.SetLayerName(gl, lyrName);

			if(map != null) {
				gl.Initialize();
				map.Layers.Add(gl);
				MapApplication.SetLayerName(gl, layerName);
			}
			return gl;
		} // public static GraphicsLayer makeRLLayer(Map map, string layerID, string layerName)
	} // class VLayer


	/// <summary>
	/// IsolatedStorageFile create/open/delete, read/write
	/// </summary>
	public class VFileStorage {
		//%userprofile%\Local Settings\Application Data\Microsoft\Silverlight\is\u22gdwu1.ozf\sdm4x5sn.0mv\1\s\jyfifwlyvf1fa5sf0ffkrpfot2evt0jbqi5zbcyw1n0tkwgndraaagaa\f\__LocalSettings
		logFunc log;
		IsolatedStorageFileStream fileStream = null;
		IsolatedStorageFile store = null;
		string dirName = @"mwb";
		// for @"str" look:
		// http://msdn.microsoft.com/en-us/library/362314fe%28v=VS.71%29.aspx

		public void nullLog(string msg) { msg.clog(); }

		public VFileStorage(logFunc logfunc) {
			log = logfunc;
			if(log == null) log = nullLog;
			log("VFileStorage.constructor, ...");
			// http://www.silverlight.net/learn/graphics/file-and-local-data/isolated-storage-%28silverlight-quickstart%29
			setFolder(dirName);
		} // public VFileStorage(logFunc logfunc)

		public void setFolder(string newDirName) {
			close();
			dirName = string.Format("{0}", newDirName);
			log(string.Format("VFileStorage.setFolder [{0}]", dirName));
			store = IsolatedStorageFile.GetUserStoreForApplication();
			if(!store.DirectoryExists(dirName)) {
				store.CreateDirectory(dirName);
			}
		} // public void setFolder(string newDirName)

		~VFileStorage() {
			close();
		}


		public void close() {
			if(fileStream != null) fileStream.Close();
			fileStream = null;
			if(store != null) store.Dispose();
		} // public void close()


		public void create(string filename) {
			log(string.Format("VFileStorage.create/write, fname [{0}]", filename));
			if(fileStream != null) fileStream.Close();
			fileStream = store.OpenFile(dirName + "/" + filename,
				FileMode.OpenOrCreate, FileAccess.Write);
			fileStream.SetLength(0); fileStream.Flush(true);
			log(string.Format("VFileStorage.create, stream opened, ready for write [{0}]", fileStream.CanWrite));
		} // public void create(string filename)


		public void open(string filename) { // open file //stor.open(filename);
			log(string.Format("VFileStorage.open/read, fname [{0}]", filename));
			if(fileStream != null) fileStream.Close();
			fileStream = store.OpenFile(dirName + "/" + filename,
				FileMode.Open, FileAccess.Read);
			log(string.Format("VFileStorage.open, stream opened"));
		} // public void open(string filename) { // open file //stor.open(filename);


		public static VFileStorage openStorage4Read(string foldername, string filename) {
			//log("openStorage4Read, ...");
			var res = new VFileStorage(null);
			res.setFolder(foldername);
			res.open(filename);
			return res;
		} // public static VFileStorage openStorage4Read(string foldername, string filename)


		public static VFileStorage openStorage4Write(string foldername, string filename) { // create and init storage object
			//log("openStorage4Write, ...");
			var res = new VFileStorage(null);
			res.setFolder(foldername);
			res.create(filename);
			return res;
		} // public static VFileStorage openStorage4Write(string foldername, string filename) { // create and init storage object


		public void delete(string filename) {
			//http://msdn.microsoft.com/en-us/library/system.io.isolatedstorage.isolatedstoragefile.deletefile%28v=vs.95%29.aspx
			log(string.Format("VFileStorage.delete, fname [{0}]", filename));
			try {
				if(fileStream != null) fileStream.Close();
				fileStream = null;
				store.DeleteFile(dirName + "/" + filename);
			}
			catch(Exception ex) {
				//catch(IsolatedStorageException ex) {
				log(string.Format("VFileStorage.delete, error [{0}]", ex.Message));
				throw new Exception("Удалить файл не удалось, вероятно он заблокирован. \n Попробуйте еще раз через некоторое время.");
			}
		} // public void delete(string filename)


		public List<string> getFilesList() {
			var res = new List<string>();
			string[] lst = store.GetFileNames(dirName + "/*");
			foreach(var s in lst) {
				res.Add(s);
			}
			return res;
		} // public List<string> getFilesList()


		public void write(string data) {
			log("VFileStorage.save, ...");
			byte[] buf = System.Text.Encoding.UTF8.GetBytes(data);
			if(store.AvailableFreeSpace < buf.Length) {
				if(!store.IncreaseQuotaTo(store.Quota + buf.Length)) {
					throw new Exception(string.Format(
						"Для записи нужно {0} байт места, но зарезервировать его невозможно!",
						store.Quota + buf.Length));
				}
			}
			fileStream.Write(buf, 0, buf.Length);
			fileStream.Flush(true);
			log("VFileStorage.save done.");
		} // public void write(string data)


		public string read() { // read from file to string //string cfg = stor.read();
			var len = fileStream.Length;
			var buf = new byte[len];
			var res = fileStream.Read(buf, 0, (int)len);
			if(res < len) {
				throw new Exception(string.Format(
					"VFileStorage.read, readed only {1} bytes from {0}", len, res));
			}
			var str = System.Text.Encoding.UTF8.GetString(buf, 0, (int)len);
			return str;
		} // public string read() { // read from file to string //string cfg = stor.read();
	} // public class VFileStorage


	/// <summary>
	/// Layer parameters from layersRepository
	/// </summary>
	public class VLayerDescription: Object {
		public string id, name, type, topic, url, proxy;

		private string _preview = "";
		public string preview {
			get {
				if(_preview == "") { return "preview/default.png"; }
				return _preview;
			}
			set { _preview = value; }
		}

		// http://help.arcgis.com/en/webapi/silverlight/apiref/ESRI.ArcGIS.Client~ESRI.ArcGIS.Client.ArcGISDynamicMapServiceLayer~ImageFormat.html
		// PNG24 JPG PNG8 PNG32
		private string _imageFormat = "";
		public string imageFormat {
			get {
				if(_imageFormat == "") { return "PNG24"; }
				return _imageFormat;
			}
			set { _imageFormat = value; }
		}

		public string printedName { get { return ToString(); } }

		override public string ToString() {
			return String.Format("{1} ({0})", id, name);
		}
		public string toString() {
			return String.Format("id [{0}], name [{1}], type [{2}], topic [{3}], url [{4}], proxy [{5}], preview [{6}], ImageFormat [{7}]",
				id, name, type, topic, url, proxy, preview, imageFormat);
		}
		public string getFromXml(XAttribute attr) { //ld.proxy = ld.getFromXml(r.Attribute("proxy"));
			if(attr == null) return "";
			return attr.Value.Trim();
		}
	} // public class VLayerDescription: Object {


	/// <summary>
	/// Queue with interlock. Helps with async WebClient tasks
	/// </summary>
	public class VConcurrentQueue {
		public bool hasProcessor = false;
		private Queue<object> _qu = new Queue<object>(333);
		private object syncO = new object(); //http://msdn.microsoft.com/en-us/library/de0542zz%28v=vs.95%29.aspx

		public void setLock() {
			System.Threading.Monitor.Enter(syncO);
		}
		public void releaseLock() {
			System.Threading.Monitor.Exit(syncO);
		}

		public void Enqueue(object o) {
			System.Threading.Monitor.Enter(syncO);
			try {
				_qu.Enqueue(o);
			}
			finally {
				System.Threading.Monitor.Exit(syncO);
			}
		}

		public object Dequeue() {
			object o = null;
			System.Threading.Monitor.Enter(syncO);
			try {
				if(_qu.Count > 0) o = _qu.Dequeue();
			}
			finally {
				System.Threading.Monitor.Exit(syncO);
			}
			return o;
		}

		public object Peek() {
			object o = null;
			System.Threading.Monitor.Enter(syncO);
			try {
				if(_qu.Count > 0) o = _qu.Peek();
			}
			finally {
				System.Threading.Monitor.Exit(syncO);
			}
			return o;
		}
	} // public class VConcurrentQueue


	/// <summary>
	/// log to browser console (ie8 script console or FF firebug)
	/// </summary>
	public static class VExtClass { // http://kodierer.blogspot.com/2009/05/silverlight-logging-extension-method.html
		/// <summary>
		/// if you are using Firefox with the Firebug add-on or
		/// Internet Explorer 8: Use the console.log mechanism
		/// </summary>
		/// <param name="obj"></param>
		public static void clog(this object obj) {
			try {
				HtmlWindow window = HtmlPage.Window;
				var isConsoleAvailable = (bool)window.Eval(
					"typeof(console) != 'undefined' && typeof(console.log) != 'undefined'");
				if(isConsoleAvailable == false) return;

				//var console = (window.Eval("console.log") as ScriptObject);
				var console = (window.Eval("slLog") as ScriptObject);
				//DateTime dt = DateTime.Now;
				//var txt = string.Format("{0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), obj);
				var txt = string.Format("{0}\n", obj);
				console.InvokeSelf(txt);
			}
			catch(Exception ex) {
				var msg = ex.Message;
				//MessageBox.Show(msg);
			}
		} // public static void clog(this object obj)


		public static string computeSHA1Hash(string val) {
			byte[] data = System.Text.Encoding.UTF8.GetBytes(val);
			System.Security.Cryptography.SHA1 sha = new System.Security.Cryptography.SHA1Managed();
			byte[] res = sha.ComputeHash(data);
			return System.BitConverter.ToString(res).Replace("-", "").ToUpper();
		} // public static string computeSHA1Hash(string val)

	} // public static class VExtClass


	public delegate void logFunc(string msg); // http://msdn.microsoft.com/en-us/library/ms173172%28v=VS.80%29.aspx


	///////////////////////////////////////////////////////////////////////
	// serialize GraphicLayer http://forums.arcgis.com/threads/8774-save-layer-to-xml-file
	///////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Class for serializing a GraphicCollection.
	/// </summary>
	[CollectionDataContract(Name = "Graphics", ItemName = "Graphic")]
	public class SerializableGraphicCollection: List<SerializableGraphic> {
		public SerializableGraphicCollection() { }

		public SerializableGraphicCollection(GraphicCollection graphicCollection) {
			foreach(Graphic g in graphicCollection) {
				Add(new SerializableGraphic(g));
			}
		}
	} // public class SerializableGraphicCollection : List<SerializableGraphic>


	/// <summary>
	/// Class for serializing a Graphic.
	/// </summary>
	[DataContract]
	public class SerializableGraphic {
		public SerializableGraphic() { }

		public SerializableGraphic(Graphic graphic) {
			Geometry = graphic.Geometry;
			Attributes = new SerializableAttributes(graphic.Attributes);
		}

		[DataMember]
		public SerializableAttributes Attributes;

		[DataMember]
		public ESRI.ArcGIS.Client.Geometry.Geometry Geometry;

		/// <summary>
		/// Gets the underlying graphic (useful after deserialization).
		/// </summary>
		/// <value>The graphic.</value>
		internal Graphic Graphic {
			get {
				Graphic g = new Graphic() { Geometry = Geometry };
				foreach(KeyValuePair<string, object> kvp in Attributes) {
					g.Attributes.Add(kvp);
				}
				return g;
			}
		}
	} // public class SerializableGraphic


	/// <summary>
	/// Class for serialization of Attributes.
	/// </summary>
	[CollectionDataContract(ItemName = "Attribute")]
	public class SerializableAttributes: List<KeyValuePair<string, object>> {
		public SerializableAttributes() { }

		public SerializableAttributes(IEnumerable<KeyValuePair<string, object>> items) {
			foreach(KeyValuePair<string, object> item in items)
				Add(item);
		}
	} // public class SerializableAttributes : List<KeyValuePair<string, object>>


	/// <summary>
	/// GraphicsLayer extension class to serialize/deserialize to XML the graphics of a graphics/feature layer
	/// Note : the symbols of the graphics are not serialized (==> working well if there is a renderer but not working without renderer (except if the symbol is initialized by code after deserialization))
	/// </summary>
	public static class GraphicsLayerExtension {
		public static void SerializeGraphics(this GraphicsLayer graphicsLayer, XmlWriter writer) {
			XMLSerialize(writer, new SerializableGraphicCollection(graphicsLayer.Graphics));
		}


		public static void DeserializeGraphics(this GraphicsLayer graphicsLayer, XmlReader reader) {
			foreach(SerializableGraphic g in XMLDeserialize<SerializableGraphicCollection>(reader)) {
				graphicsLayer.Graphics.Add(g.Graphic);
			}
		}


		private static void XMLSerialize<T>(XmlWriter writer, T data) {
			var serializer = new DataContractSerializer(typeof(T));
			serializer.WriteStartObject(writer, data);

			// Optimizing Away Repeat XML Namespace Declarations
			writer.WriteAttributeString("xmlns", "sys", null, "http://www.w3.org/2001/XMLSchema");
			writer.WriteAttributeString("xmlns", "esri", null, "http://schemas.datacontract.org/2004/07/ESRI.ArcGIS.Client.Geometry");
			writer.WriteAttributeString("xmlns", "col", null, "http://schemas.datacontract.org/2004/07/System.Collections.Generic");

			serializer.WriteObjectContent(writer, data);
			serializer.WriteEndObject(writer);
		}


		private static T XMLDeserialize<T>(XmlReader reader) {
			var serializer = new DataContractSerializer(typeof(T));
			T data = (T)serializer.ReadObject(reader);
			return data;
		}
	} // public static class GraphicsLayerExtension
	///////////////////////////////////////////////////////////////////////
	// serialize GraphicLayer
	///////////////////////////////////////////////////////////////////////


	public class VSymbol {
		// using this in xaml, lists of named symbols
		public string DisplayName { get; set; }
		public ESRI.ArcGIS.Client.Symbols.Symbol symbol { get; set; }
	}
	public class VTwoNames {
		// using this in xaml, lists of named symbols
		public string DisplayName { get; set; }
		public string keyName { get; set; }
		//public ESRI.ArcGIS.Mapping.Core.Symbols.SimpleFillSymbol areaSymb { get; set; }
	}


	/// <summary>
	/// VLayer may contain relations
	/// </summary>
	public class VRelationInfo {
		//relationsListForm.listBox1.Items.Add(string.Format("linkID: {0}, linkName: {1}, tableID: {2}", 
		// r.Id, r.Name, r.RelatedTableId));
		//var rels = relatesLayer.LayerInfo.Relationships;
		//var r = rels.First();
		public string name, descr;
		public int id, tableId;
		public ESRI.ArcGIS.Client.FeatureService.Relationship relObj;
		public Dictionary<int, IEnumerable<Graphic>> linkedRecords = null; // new Dictionary<int, IEnumerable<Graphic>>(); // featureOID, linkedrecords
		public int oid = 0;

		public override string ToString() {
			var cnt = this.getRelatedCount(oid);
			if(cnt == -1) return name;
			return string.Format("{0} (кол-во: {1})", name, cnt);
		}

		public int getRelatedCount(int objid) {
			if(this.linkedRecords == null) return -1;
			IEnumerable<Graphic> recs = null;
			if(this.linkedRecords.TryGetValue(objid, out recs)) {
				return recs == null ? 0 : recs.Count();
			}
			return -1;
		}

		public VRelationInfo(ESRI.ArcGIS.Client.FeatureService.Relationship rel) {
			relObj = rel;
			id = rel.Id;
			name = rel.Name;
			tableId = rel.RelatedTableId;
			descr = string.Format("linkID: {0}, linkName: {1}, tableID: {2}", id, name, tableId);
		} // public VRelationInfo(ESRI.ArcGIS.Client.FeatureService.Relationship rel)


		public VRelationInfo(string description) {
			descr = description;
			relObj = null;
			id = -1;
			name = "";
			tableId = -1;

			string[] parts = descr.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
			if(parts.Length != 3) throw new Exception("VRelationInfo, malformed relation description" + ": " + descr);
			foreach(var part in parts) {
				var items = part.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries);
				if(items.Length != 2) throw new Exception("VRelationInfo, malformed relation description" + ": " + descr);

				if(items[0] == "linkID") {
					id = Int32.Parse(items[1]);
				}
				else if(items[0] == "linkName") {
					name = items[1];
				}
				else if(items[0] == "tableID") {
					tableId = Int32.Parse(items[1]);
				}
				else {
					throw new Exception("VRelationInfo, malformed relation description" + ": " + descr);
				}
			}
		} // public VRelationInfo(string description)

	} // public class VRelationInfo

} // namespace mwb02.AddIns
