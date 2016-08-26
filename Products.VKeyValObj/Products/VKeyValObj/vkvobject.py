#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

__doc__ = """VKeyValObj initialization module. """
__version__ = '1.0'

from zope.interface import Interface

class IVKVObject(Interface):
	"""Simple key-value object
	"""

	def getValue():
		"""Get object value.
		"""


from Acquisition import Implicit
from Globals import Persistent
from AccessControl.Role import RoleManager
from OFS.SimpleItem import Item
from OFS.PropertyManager import PropertyManager

from AccessControl import ClassSecurityInfo
from Globals import InitializeClass

from zope.interface import implements

from Products.PageTemplates.PageTemplateFile import PageTemplateFile # ZPT files
from Globals import DTMLFile # dtml files

import logging
logger = logging.getLogger('VKVObject')

addForm = DTMLFile('www/vkvoFileAdd', globals())

def addFunction(self, id, text, REQUEST=None):
	"""addForm processor function. Constructor.
	Create a new object and add it to container.
	"""
	p = VKVObject(id, text)
	self.Destination()._setObject(id, p)
	if REQUEST is not None:
		return self.manage_main(self, REQUEST, update_menu=0)
	return id


class VKVObject(Implicit, Persistent, RoleManager, Item, PropertyManager):
	"""VKVObject product class, implements IVCUFile
	"""
	implements(IVKVObject)

	#Item requerements
	meta_type = 'VKVObject'
	id = ''
	title = ''

	# ZMI views
	manage_options = (
		{'label':'Edit',   'action': 'editForm'}, # editForm invoke editFunction
		{'label':'View',   'action': ''}, # If your class has a default view method (index_html) you should also include a View view whose action is an empty string.
	) + PropertyManager.manage_options + RoleManager.manage_options + Item.manage_options # properties, security, undo, ownership

	security = ClassSecurityInfo()

	security.declareProtected('View', 'index_html')
	index_html = PageTemplateFile('www/vkvoFileView', globals()) # ZPT file

	security.declareProtected('Change Images and Files', 'editForm')
	editForm = DTMLFile('www/vkvoFileEdit', globals()) # DTML file


	security.declareProtected('Change Images and Files', 'editFunction')
	def editFunction(self, text, REQUEST=None):
		"""editForm processor. Changes attributes."""
		self.text = text
		if REQUEST is not None:
			return self.editForm(REQUEST, management_view='Edit',
				manage_tabs_message='VKVObject attribs changed.')


	def __init__(self, id, text):
		logger.debug('VKVObject.__init__')
		self.id = id
		self.text = text


	security.declarePublic('getValue')
	def getValue(self):
		"""File value"""
		return self.text

#class VKVObject(Implicit, Persistent, RoleManager, Item)

InitializeClass(VKVObject)

if __name__ == "__main__":
	import doctest
	doctest.testmod(verbose=True)
