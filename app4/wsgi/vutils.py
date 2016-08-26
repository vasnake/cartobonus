#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

""" Flask web app utils for Cartobonus

"""

import sys, string, os, time

from flask import (Flask, url_for, render_template, make_response, request, abort,
	flash, redirect, session, request_started, send_from_directory)
from flask.ext.login import (LoginManager, current_user, login_required,
	login_user, logout_user, UserMixin, AnonymousUser,
	confirm_login, fresh_login_required)
from functools import update_wrapper


def getTellyDirAndName(app):
	""" return (dirname, filename) for user telly file
	"""
	baseDir = os.path.abspath(os.path.join(app.root_path, '..'))
	fname = app.config['LAYERSLIST_XML']
	if current_user.is_authenticated():
		fname = app.config['TMPL_LAYERSLIST_XML'] % current_user.id
	if not os.path.exists(os.path.join(baseDir, fname)):
		fname = app.config['LAYERSLIST_XML']
	return (baseDir, fname)
#def getTellyDirAndName():


def nocache(f):
	""" decorator for cache_control
	"""
	def new_func(*args, **kwargs):
		resp = make_response(f(*args, **kwargs))
		resp.cache_control.no_cache = True
		return resp
	return update_wrapper(new_func, f)
#def nocache(f):


def getKVFromRawData(data='id=foo&amp;text=bar'):
	""" parse kvsproxy (for keyvalstor) input data

	return (key, val)
	"""
	args = data.split('&amp;text=', 1) # [id=foo, bar]
	if not len(args) == 2:
		args = data.split('&text=', 1)
		if not len(args) == 2:
			return ('', '')
	key = args[0][3:]
	val = args[1]
	return (key, val)
#def getKVFromRawData(data='id=foo&amp;text=bar'):


def getWsgiInput():
	""" get stream content from request.environ['wsgi.input']
	must be call only from POST handler before any data access

	return raw request input data
	"""
	if not request.method == 'POST': return ''
	env = request.environ
	stream = env['wsgi.input']
	cl = env.get('CONTENT_LENGTH', '0')
	cl = 0 if cl == '' else int(cl)
	if cl == 0: return ''
	return stream.read(cl)
#def getWsgiInput():


def doDocTest():
	''' http://docs.python.org/library/doctest.html
	http://stackoverflow.com/questions/1733414/how-do-i-include-unicode-strings-in-python-doctests
	'''
	import sys
	reload(sys)
	sys.setdefaultencoding("UTF-8")
	import doctest
	doctest.testmod(verbose=True)

if __name__ == '__main__':
	doDocTest()
