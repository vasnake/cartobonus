#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

""" Flask web app for Cartobonus
http://flask.pocoo.org/docs/quickstart/#quickstart

$ set path=%path%;c:\d\Python27;c:\d\Python27\Scripts
$ pip install Flask flask-login blinker

todo:
	bugfix and code cleaning

"""

import sys, string, os, time
import logging

from flask import (Flask, url_for, render_template, make_response, request, abort,
	flash, redirect, session, request_started, send_from_directory)
from flask.ext.login import (LoginManager, current_user, login_required,
	login_user, logout_user, UserMixin, AnonymousUser,
	confirm_login, fresh_login_required)
from functools import update_wrapper

import vutils, webdisproxy, vauth

CP = 'utf-8'

################################################################################

# app setup

app = Flask(__name__)
app.config.from_object('default_settings')
#~ app.config.from_object(__name__) # USERS
log = app.logger

login_manager = LoginManager()
login_manager.anonymous_user = vauth.Anonymous
login_manager.login_view = "login"
login_manager.login_message = u"Для доступа к этой странице вам надо зарегистрироваться."
login_manager.refresh_view = "reauth"
login_manager.session_protection = "strong"

@login_manager.user_loader
def load_user(id):
	return vauth.USERS.get(int(id))

login_manager.setup_app(app)

def updateSession(sender, **extra):
	""" on before request - update session data
	"""
	hits = session.get('hits', 0)
	session['hits'] = hits + 1
	sc = session.get('sessionCreated', '')
	if not sc: session['sessionCreated'] = time.strftime('%Y-%m-%d %H:%M:%S')
request_started.connect(updateSession, app)

# end app setup

################################################################################

# pages

@app.route('/')
def mainPage():
	""" list of all CB servlets, for demo purposes only
	"""
	lst = [url_for('login')]
	for x in ('reauth', 'logout', 'kvsproxyPage', 'cabinetPage', 'mapservicesPage', 'edittellyPage', 'savetellyPage'):
		lst.append(url_for(x))
	return render_template('servlets.html', lst=lst)

################################################################################
# Flask-login part

@app.route("/login", methods=["GET", "POST"])
def login():
	""" login form render (GET) and form processor (POST)
	"""
	if request.method == "POST" and "username" in request.form:
		username = request.form.get("username", '')
		password = request.form.get("password", '')
		remember = request.form.get("remember", "no").lower() == "yes"
		user = vauth.getUser(username, password)
		if user:
			if login_user(user, remember=remember):
				#~ flash(u"Регистрация прошла успешно!")
				return redirect(request.args.get("next") or url_for("cabinetPage"))
			else:
				flash(u"Ваша регистрация заморожена, вы не сможете зарегистрироваться.")
		else:
			flash(u"Вы не угадали имя и/или пароль.")
	return render_template("login.html")
#def login():


@app.route("/reauth", methods=["GET", "POST"])
@login_required
def reauth():
	""" confirm_login sets the current session as fresh. Sessions become stale when they are reloaded from a cookie.
	"""
	if request.method == "POST":
		confirm_login()
		#~ flash(u"Регистрация обновлена.")
		return redirect(request.args.get("next") or url_for("cabinetPage"))
	return render_template("reauth.html")


@app.route("/logout")
@login_required
def logout():
	logout_user()
	flash(u"Вы успешно вышли из сессии.")
	return redirect(url_for("cabinetPage"))

# Flask-login part
################################################################################


@app.route('/kvsproxy', methods=['GET', 'POST'])
def kvsproxyPage():
	"""	Key-value service proxy.
	Serve two types of queries, get and set.
	Redirect query to webdis service.

	get query
	http://www.allgis.org/cartobonus.servlets/kvsproxy?get=foo
	-> http://keyvalstor.algis.com:7379/GET/foo.txt

	set (POST) query
	http://www.allgis.org/cartobonus.servlets/kvsproxy?id=foo&text=bar/rab.arb
	-> http://keyvalstor.algis.com:7379/SET/foo/bar%2Frab%2Earb

	>>> import urllib
	>>> url = 'http://www.allgis.org/cartobonus.servlets/kvsproxy'
	>>> res = urllib.urlopen(url, 'id=foo&amp;text=bar')
	>>> print urllib.urlopen('%s?get=foo' % url).read()
	bar
	>>> res = urllib.urlopen(url, "id=foo&amp;text=% %2f and %2e . ! # $ & ' ( ) * + , / : ; = ? @ [ ] %21 %23 %24 %26 %27 %28 %29 %2A %2B %2C %2F %3A %3B %3D %3F %40 %5B %5D &amp; &lt; &gt; &lquo; &rquo; tags <a>b</a>, urls: http://site/o <!-- comments -->")
	>>> print urllib.urlopen('%s?get=foo' % url).read()
	% %2f and %2e . ! # $ & ' ( ) * + , / : ; = ? @ [ ] %21 %23 %24 %26 %27 %28 %29 %2A %2B %2C %2F %3A %3B %3D %3F %40 %5B %5D &amp; &lt; &gt; &lquo; &rquo; tags <a>b</a>, urls: http://site/o <!-- comments -->
	"""
	kvs = webdisproxy.VKeyValStor(app.config['KVSBASEURL'])
	answer = ''

	# if SET
	if request.method == 'POST':
		rawdata = vutils.getWsgiInput()
		app.logger.debug("kvsproxyPage POST raw data '%s'" % rawdata) # POST raw data 'id=foo&amp;text=bar'
		key, val = vutils.getKVFromRawData(rawdata)
		# data parsing not working in no way, because data was sent without urlencode
		#~ key = request.form.get('id', '')
		#~ val = request.form.get('text', '')
		if key == '':
			return render_template('badrequest.html',
				msg='wrong POST data: key [%s], val [%s], rawdata [%s]' % (key, val, rawdata)), 400
			#~ abort(400)
			#~ raise NameError('wrong POST data: key [%s], val [%s], rawdata [%s]' % (key, val, rawdata))
		answer = kvs.set(key, val)

	# if GET
	else:
		app.logger.debug("kvsproxyPage request args '%s'" % request.args)
		key = request.args.get('get', '')
		if key == '':
			return render_template('badrequest.html',
				msg='wrong GET data [%s]' % request.args), 400
		answer = kvs.get(key)

	resp = make_response(answer, 200)
	resp.headers['Content-Type'] = 'text/plain; charset=utf-8'
	return resp
#def kvsproxyPage():


@app.route('/cabinet')
@fresh_login_required
def cabinetPage():
	"""show user profile or auth page
	"""
	return render_template("cabinet.html")
#def cabinetPage():


@app.route('/mapservices.list.xml')
@vutils.nocache
def mapservicesPage():
	""" goto custom telly
	send mapservices.list.xml according userId
	Content-Type: application/xml
	"""
	baseDir, fname = vutils.getTellyDirAndName(app)
	return send_from_directory(baseDir, fname, mimetype='application/xml')
#def mapservicesPage():


@app.route('/edittelly')
@fresh_login_required
def edittellyPage():
	""" edit form for mapservices.list.xml according userId
	"""
	baseDir, fname = vutils.getTellyDirAndName(app)
	fo = open(os.path.join(baseDir, fname))
	xml = fo.read().decode(CP)
	fo.close()
	return render_template('edittelly.html', xml=xml)
#def edittellyPage():


@app.route('/savetelly', methods=["GET", "POST"])
@fresh_login_required
def savetellyPage():
	"""save user telly from edit form
	"""
	if request.method == 'GET':
		return redirect(url_for("edittellyPage"))

	baseDir, fname = vutils.getTellyDirAndName(app)
	fname = app.config['TMPL_LAYERSLIST_XML'] % current_user.id
	xml = request.form.get('telly', (u'<?xml version="1.0" encoding="utf-8"?>'))

	fo = open(os.path.join(baseDir, fname), 'wb')
	fo.write(xml.encode(CP))
	fo.close()
	flash(u"Данные сохранены успешно")

	return redirect(url_for("edittellyPage"))
#def savetellyPage():


@app.route('/favicon.ico')
def favicon():
	return send_from_directory(os.path.join(app.root_path, 'static'),
	   'favicon.ico', mimetype='image/vnd.microsoft.icon')


def setLogger(log):
	''' debug logger, http://docs.python.org/howto/logging-cookbook.html
	logrotate
	http://stackoverflow.com/questions/8467978/python-want-logging-with-log-rotation-and-compression
	http://docs.python.org/library/logging.handlers.html#rotatingfilehandler
	'''
	logFilename = app.config['LOGFILENAME']
	log.setLevel(logging.DEBUG)
	formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
	if not hasattr(log, 'vFHandle'):
		#~ import logging.handlers as handlers
		# 5 files up to 1 megabyte each
		#~ fh = handlers.RotatingFileHandler(logFilename, maxBytes=1000000, backupCount=5, encoding='utf-8')
		#~ fh = handlers.TimedRotatingFileHandler(logFilename, backupCount=3, when='M', interval=3, encoding='utf-8',)
		fh = logging.FileHandler(logFilename)
		#~ fh = logging.NullHandler()
		fh.setLevel(logging.DEBUG)
		fh.setFormatter(formatter)
		log.addHandler(fh)
		log.vFHandle = fh
	#~ print 'Log configured. Look file [%s] for messages' % logFilename
#def setLogger(log):


if __name__ == '__main__':
	setLogger(log)
	app.run(debug=True)
