#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

r"""
mod_wsgi app (see 'def application(environ, start_response):') for Cartobonus.

http://code.google.com/p/modwsgi/wiki/InstallationOnWindows
http://code.google.com/p/modwsgi/wiki/ConfigurationGuidelines
http://flask.pocoo.org/docs/deploying/mod_wsgi/

httpd.conf
	#WSGIScriptAliasMatch ^/cartobonus\..* "c:/Inetpub/wwwroot/Apps/app4/wsgi/pycb.wsgi"
	WSGIScriptAlias "/cartobonus.servlets" "c:/Inetpub/wwwroot/Apps/app4/wsgi/pycb.wsgi"

.htaccess contents
	Options None
	Order allow,deny
	Allow from all
	RemoveHandler .py
	<FilesMatch "\.(pyo|pyc|py)$">
		SetHandler None
		Order deny,allow
		Deny from all
	</FilesMatch>
	LimitRequestBody 10485760
	AddDefaultCharset utf-8
	WSGIScriptReloading On
	SetEnv pycb.mailhost mail.allgis.org

"""

import os, sys

#~ pth = 'c:/Inetpub/wwwroot/Apps/app4/wsgi'
pth = os.path.dirname(__file__)
if pth not in sys.path:
	sys.path.insert(0, pth)

# get modwsgi application from Flask module
#~ from test import app as application
from servlets_controller import app as application


def testapplication(environ, start_response):
	""" A very simple WSGI application
	http://code.google.com/p/modwsgi/wiki/QuickConfigurationGuide
	"""
	status = '200 OK'

	output = u'Привет Мир! \n environ: \n'.encode('utf-8')
	for x in sorted(environ.keys()):
		output += "'%s' : '%s' \n" % (x, environ[x])
	output += 'sys.version = %s \n' % repr(sys.version)
	output += 'sys.prefix = %s \n' % repr(sys.prefix)
	output += 'sys.path = %s \n' % repr(sys.path)
	output += 'os.path.abspath(__file__) = %s \n' % os.path.abspath(__file__)

	if environ.has_key('mod_wsgi.version'):
		output += 'mod_wsgi rulez \n'
	else:
		output = 'other (not mod_wsgi) WSGI hosting mechanism! \n'

	response_headers = [('Content-type', 'text/plain; charset=utf-8'), ('Content-Length', str(len(output)))]
	start_response(status, response_headers)
	return [output]
