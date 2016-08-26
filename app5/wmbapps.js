//# -*- mode: javascript; coding: utf-8 -*-
//# (c) Valik mailto:vasnake@gmail.com

/*
if(console){;} else{
	var console = new Object();
	console.log = function(v){window.status=v;};
	window.console = console;
}
*/

function log(str, doalert) { // write app messages to log
	try {
		var x = new Date();
		var y = x.toUTCString()+': '+str;
		console.log(y);
		if(doalert && doalert === true) alert(str);
	}catch(ex){
		window.status = 'log error: ' + ex.description + '; Log msg: ' + str;
	}
}


function slLog(str) { // write app messages to log
	try {
		console.log(str);
	}catch(ex){
		window.status = 'slLog error: ' + ex.description + '; Log msg: ' + str;
	}
}


/*
HtmlWindow window = HtmlPage.Window;
var func = (window.Eval("saveMap2Server") as ScriptObject);
func.InvokeSelf(servUrl, hash, currentCfg);
*/
function saveMap2Server(servUrl, hash, cfg) {
	log("js.saveMap2Server, servUrl '"+servUrl+"', key '"+hash+"', conf '"+cfg+"'");
	location.hash = hash
	//http://localhost:8080/t/keyvalstor/manage_addProduct/VKeyValObj/addFunction?id=foo&text=bar
	var data = { id: hash, text: cfg }
	// Due to browser security restrictions, most "Ajax" requests are subject to the same origin policy; the request can not successfully retrieve data from a different domain, subdomain, or protocol.
	return;
	jQuery.post(servUrl + '/manage_addProduct/VKeyValObj/addFunction',
		data,
		processSrvResp
	).fail(processSrvError);
} // function saveMap2Server() {


function processSrvError(xhr, etype, ex) { // on jQuery.ajax.srv.error
	try {
		var msg = "Error in HTTP POST: \n status: " + xhr.status + "; \n " + etype + "; \n " + ex;
		log('js.jQuery.post.fail: ' + msg);
	} catch (ex) {
		log(ex.description);
	}
} // function processSrvError(xhr, etype, ex) {


function processSrvResp(data, stat, xhr) { // on jQuery.ajax.resp.done
	try{
		log('js.processSrvResp, data ['+data+']; stat ['+stat+']; xhr ['+xhr+']');
	} catch (ex) {
		log('js.processSrvResp, Error: '+ex.description);
	}
} // function processSrvResp(data, stat, xhr) {


function getLocationHash() {
	return location.hash.replace('#', '');
} // function getLocationHash()
