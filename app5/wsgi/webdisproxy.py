#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

""" keyvalstor get,set methds for Cartobonus

"""

import urllib

class VKeyValStor(object):
	def __init__(self, backendurl):
		self.webdisUrl = backendurl

	def set(self, key, val):
		""" Create 'set' request to webdis, send it and return answer.

		POST / with COMMAND/arg0/.../argN in the HTTP body. (https://github.com/nicolasff/webdis)
		Special characters: / and . have special meanings, / separates arguments and . changes the Content-Type.
		They can be replaced by %2f and %2e, respectively
		Reserved characters after percent-encoding (http://en.wikipedia.org/wiki/Percent-encoding)
		"""
		v = val.replace('%', '%25').replace("/", "%2F").replace(".", "%2E").replace("?", "%3F").replace('+', '%2B')
		#~ v = urllib.urlencode(val) # maybe I should encode whole data
		data = 'SET/%s/%s' % (key, v)
		res = urllib.urlopen('%s/' % self.webdisUrl, data)
		answer = '%s' % res.read()
		return answer
#	def set(self, key, val):


	def get(self, key):
		""" get data from webdis by key
		"""
		res = urllib.urlopen('%s/GET/%s.txt' % (self.webdisUrl, key))
		answer = '%s' % res.read()
		return answer
#	def get(self, key):
#class VKeyValStor(object):


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
