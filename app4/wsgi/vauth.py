#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

""" Flask web app authentication
http://packages.python.org/Flask-Login/#api-documentation
"""

from flask import (Flask, url_for, render_template, make_response, request, abort,
	flash, redirect, session)
from flask.ext.login import (LoginManager, current_user, login_required,
	login_user, logout_user, UserMixin, AnonymousUser,
	confirm_login, fresh_login_required)


class Anonymous(AnonymousUser):
	name = u"Anonymous"

class User(UserMixin):
	def __init__(self, name, id, password, fullname, active=True):
		self.name = name
		self.id = id
		self.active = active
		self.password = password
		self.fullname = fullname
	def is_active(self):
		return self.active
#class User(UserMixin):

USERS = {
	1: User(u"guest", 1, u'guest', u'Иванов Иван Иванович'),
	2: User(u'valik', 2, u'kilav', u'Федулов Валентин Николаевич'),
	3: User(u'parilov', 3, u'volirap', u'Парилов Алексей Юрьевич'),
}

def getUser(username, password):
	for x in USERS.keys():
		u = USERS[x]
		if u.name.lower() == username.lower() and u.password == password:
			return u
	return None
#def getUser(username, password):


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
