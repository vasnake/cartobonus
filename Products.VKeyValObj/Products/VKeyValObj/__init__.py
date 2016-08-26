#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

__doc__ = """VKeyValObj initialization module. """
__version__ = '1.0'

from vkvobject import VKVObject, addForm, addFunction

def initialize(registrar):
	"""Initializer called when used as a Zope 2 product."""
	registrar.registerClass(
		VKVObject,
		constructors=(addForm, addFunction),
		icon = 'www/vkvofile16.png'
	)
	# help notes, by example (c:\Zope\2.11.4\Zope\lib\python\Products\ExternalMethod\)
	registrar.registerHelp()
	registrar.registerHelpTitle('Zope Help')
