#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

""" Flask web app example and tests
http://flask.pocoo.org/docs/quickstart/#quickstart
"""

from flask import Flask
app = Flask(__name__)

@app.route('/')
def mainPage():
	return u'Flask: каталог сервлетов?'

@app.route('/help')
def helpPage():
	return u'Flask: справка по сервлетам?'


def kvsproxyTest():
	"""	Key-value service proxy test.
	Serve two types of queries, get and set.
	Redirect query to webdis service.

	get query
	http://www.allgis.org/cartobonus.servlets/kvsproxy?get=foo
	-> http://keyvalstor.algis.com:7379/GET/foo.txt

	set (POST) query
	http://www.allgis.org/cartobonus.servlets/kvsproxy?id=foo&text=bar/rab.arb
	-> http://keyvalstor.algis.com:7379/SET/foo/bar%2Frab%2Earb

	>>> import urllib
	>>> url = 'http://127.0.0.1:5000/kvsproxy'
	>>> #url = 'http://www.allgis.org/cartobonus/kvsproxy.py'
	>>> #url = 'http://www.allgis.org/cartobonus.servlets/kvsproxy'
	>>> # wrong set&get:
	>>> urllib.urlopen(url, '').read()
	'wrong POST data: key [], val [], rawdata []'
	>>> urllib.urlopen(url).getcode()
	400
	>>> # good set&get:
	>>> urllib.urlopen(url, 'id=foo&amp;text=bar').getcode()
	200
	>>> print urllib.urlopen('%s?get=foo' % url).read()
	bar
	>>> urllib.urlopen(url, "id=foo&amp;text=% %2f and %2e . ! # $ & ' ( ) * + , / : ; = ? @ [ ] %21 %23 %24 %26 %27 %28 %29 %2A %2B %2C %2F %3A %3B %3D %3F %40 %5B %5D &amp; &lt; &gt; &lquo; &rquo; tags <a>b</a>, urls: http://site/o <!-- comments -->").read()
	'{"SET":[true,"OK"]}'
	>>> print urllib.urlopen('%s?get=foo' % url).read()
	% %2f and %2e . ! # $ & ' ( ) * + , / : ; = ? @ [ ] %21 %23 %24 %26 %27 %28 %29 %2A %2B %2C %2F %3A %3B %3D %3F %40 %5B %5D &amp; &lt; &gt; &lquo; &rquo; tags <a>b</a>, urls: http://site/o <!-- comments -->
	"""
	pass


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
	#~ app.run()
	doDocTest()
