#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

'''
Программа загрузки в XML данных по слоям
Одна запись выглядит так:
	<Layer id="setka.map" type="ArcGISDynamicMapServiceLayer "
		name="Меридианы, параллели" topic="География">
		http://rngis.algis.com/ArcGIS/rest/services/mesh/MapServer
	</Layer>

'''

import sys, os
from readxls import VReadXLS
cp = 'utf8'


class VRecAttribs:
	def __init__(self):
		self.attrs = {'D':'name', 'E':'topic', 'F':'mxd', 'G':'url'} # if cell in ['D', 'E', 'F', 'G']:
		for k in self.attrs.values():
			self.__dict__[k] = ''

	def __str__(self):
		showList = sorted(self.attrs.values())
		return ("VRecAttribs(%i): " % id(self)) + "; "\
			.join(["%s: '%s'" % (key, self.__dict__[key]) for key in showList])

	def setAttrib(self, cell, val): # ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H']
		self.__dict__[self.attrs[cell]] = val
		if cell == 'F': # projcode, 1487.0 must be 1487
			''' ёксель упорно возвращает ячейки со строкой 1487 в виде числа
			и ничего тут не сделаешь. Пока не убедишь ёксель выдавать это данное как строку.
			'''
			pass
#class VSheetAttribs:

class VMapServicesETL:
	def __init__(self, xlsFileName):
		self.xlsData = VReadXLS(xlsFileName)
		self.dictRecs = {}

	def doWork(self):
		self.collectData()
		self.transformData()
		self.printXML()

	def collectData(self):
		print 'collectData...'
		xls = self.xlsData
		res = {}
		lines = xls.rows()
		i = 0
		for r in lines:
			i += 1
			if i == 1: continue
			rec = VRecAttribs()
			for cell, val in xls.parseRow(r):
				#~ print 'cell[%s%s]="%s"' % (cell, i, val)
				if cell in ['D', 'E', 'F', 'G']:
					rec.setAttrib(cell, val)
			res[rec.url] = rec
		self.dictRecs = res
#	def collectData(self):


	def transformData(self):
		print 'transformData...'
		res = {}
		for k in sorted(self.dictRecs.keys()):
			rec = self.dictRecs[k]
			if (rec.url.strip() == ''):
				continue
			rec.name = rec.name.strip().replace('\"', '\'')
			rec.topic = rec.topic.strip().replace('\"', '\'')
			res[k] = rec
		self.dictRecs = res
#	def transformData(self):


	def printXML(self):
		print 'printXML...'
		cnt = 0
		for k in sorted(self.dictRecs.keys()):
			cnt += 1
			rec = self.dictRecs[k] # name, topic, mxd, url
			r = '''
	<Layer id="%s" type="ArcGISDynamicMapServiceLayer"
		name="%s" topic="%s">
		%s
	</Layer>
			''' % (cnt, rec.name, rec.topic, rec.url)
			print r
#	def printXML(self):
#class VMapServicesETL (V_EA_ETL):


def main():
	print >> sys.stderr, 'argc: [%s], argv: [%s]' % (len(sys.argv), sys.argv)
	etl = VMapServicesETL(r'wmb.data.xls')
	etl.doWork()

if __name__ == "__main__":
	main()
	#~ raise NameError('not yet')

print r'Done. If next line is weird,' + \
	' something is wrong!'
print (u'если это видно, сбоев нет (%s)' % cp).encode(cp)
