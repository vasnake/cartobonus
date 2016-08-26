# -*- coding: utf-8 -*-

'''
шаг первый, прочесть данные из файлов
c:\d\code\nvs.ea.etl\data\ea.gasmain.xls
c:\d\code\nvs.ea.etl\data\ea.sheets.xls
пример данных:
cell A1: type=1, data: Номер объекта
cell B1: type=1, data: Имя файла
cell C1: type=1, data: Вид документа
cell D1: type=1, data: Дата ввода в эксплуатацию
cell E1: type=1, data: Протяжённость, км
cell F1: type=1, data: Шифр проекта
cell G1: type=1, data: Описание документа
cell H1: type=1, data: Номер папки

cell A2: type=1, data: 001
cell B2: type=1, data: 001-1-01.PDF
cell C2: type=1, data: Акт приёмки
cell D2: type=3, data: (1989, 10, 19, 0, 0, 0)
cell E2: type=2, data: 2.2999999999999998
cell F2: type=0, data: ''
cell G2: type=0, data: ''
cell H2: type=1, data: 1Р-9а

'''

import sys, os
import xlrd
cp = 'utf8'


class VReadXLS:
	bk = None
	fileName = ''
	def __init__(self, fileName):
		bk = xlrd.open_workbook(fileName)
		self.bk = bk
		self.fileName = fileName

	def rows(self, sheetidx=0):
		''' generator, like this:
		while 1:
			yield b # return and continue
			a, b = b, a+b
			if b > 100:
				return
		'''
		sh = self.bk.sheet_by_index(sheetidx)
		colrange = range(sh.ncols)
		rowrange = xrange(sh.nrows)
		for row in rowrange:
			#~ for col, ty, val, _unused in self.getRowData(sh, row, colrange):
			yield self.getRowData(sh, row, colrange)
		return
#	def rows(self, sheetidx=0):

	def parseRow(self, data):
		res = []
		for col, ty, val, _unused in data:
			value = ''
			clmn = '%s' % xlrd.colname(col)
			if val: # print not empty cell
				if ty == 1: # text
					value = "%s" % (u''+val+u'').encode(cp)
					#~ value = (u'%s' % val).encode(cp)
				elif ty == 2: # number 2.2999999999999998 / 2.3
					value = "%s" % val
				elif ty == 3: # datetime? (1989, 10, 19, 0, 0, 0) / 19.10.1989
					value = "%s-%s-%s" % (val[0], val[1], val[2])
				else:
					value = "%r" % val
			res.append((clmn.strip(), value.strip()))
		return res
#	def parseRow(self, data, rowx=1):

	def printSheet(self, sheetidx):
		bk = self.bk
		sh = bk.sheet_by_index(sheetidx)
		colrange = range(sh.ncols)
		for row in xrange(sh.nrows):
			#~ self.printRow(sh, row, range(sh.ncols))
			print
			for col, ty, val, _unused in self.getRowData(sh, row, colrange):
				self.printCell(val, ty, col, row)
#	def printSheet(self, sheetidx):

	def printCell(self, val, ty, colx, rowx):
		if val: # print not empty cell
			if ty == 1: # text
				print "p1: cell %s%d: type=%d, data: [%s]" \
					% (xlrd.colname(colx), rowx+1, ty, (u''+val+u'').encode(cp))
			elif ty == 2: # number 2.2999999999999998 / 2.3
				print "p2: cell %s%d: type=%d, data: [%s]" \
					% (xlrd.colname(colx), rowx+1, ty, val)
			elif ty == 3: # datetime? (1989, 10, 19, 0, 0, 0) / 19.10.1989
				print "p3: cell %s%d: type=%d, data: [%s-%s-%s]" \
					% (xlrd.colname(colx), rowx+1, ty, val[0],val[1],val[2])
			else:
				print "p4: cell %s%d: type=%d, data: %r" % (xlrd.colname(colx), rowx+1, ty, val)
#	def printCell(self, val, ty, colx):

	def printRow(self, sh, rowx, colrange):
		bk = self.bk
		print
		for colx, ty, val, _unused in self.getRowData(sh, rowx, colrange):
			if val: # print not empty cell
				if ty == 1: # text
					print "p5: cell %s%d: type=%d, data: [%s]" \
						% (xlrd.colname(colx), rowx+1, ty, (u''+val+u'').encode(cp))
				elif ty == 2: # number 2.2999999999999998 / 2.3
					print "p6: cell %s%d: type=%d, data: [%s]" \
						% (xlrd.colname(colx), rowx+1, ty, val)
				elif ty == 3: # datetime? (1989, 10, 19, 0, 0, 0) / 19.10.1989
					print "p7: cell %s%d: type=%d, data: [%s-%s-%s]" \
						% (xlrd.colname(colx), rowx+1, ty, val[0],val[1],val[2])
				else:
					print "p8: cell %s%d: type=%d, data: [%r]" % (xlrd.colname(colx), rowx+1, ty, val)
#	def printRow(self, sh, rowx, colrange):

	def getRowData(self, sh, rowx, colrange):
		bk = self.bk
		result = []
		dmode = bk.datemode
		ctys = sh.row_types(rowx)
		cvals = sh.row_values(rowx)
		for colx in colrange:
			cty = ctys[colx]
			cval = cvals[colx]
			if bk.formatting_info:
				cxfx = str(sh.cell_xf_index(rowx, colx))
			else:
				cxfx = ''
			if cty == xlrd.XL_CELL_DATE:
				try:
					showval = xlrd.xldate_as_tuple(cval, dmode)
				except xlrd.XLDateError:
					e1, e2 = sys.exc_info()[:2]
					showval = "%s:%s" % (e1.__name__, e2)
					cty = xlrd.XL_CELL_ERROR
			elif cty == xlrd.XL_CELL_ERROR:
				showval = xlrd.error_text_from_code.get(cval, '<Unknown error code 0x%02x>' % cval)
			else:
				showval = cval
			result.append((colx, cty, showval, cxfx))
		return result
#	def getRowData(self, sh, rowx, colrange):
#class VReadXLS:


def main():
	print >> sys.stderr, 'argc: [%s], argv: [%s]' % (len(sys.argv), sys.argv)
	#~ vrx = VReadXLS(r'data\20110406.exz.xls')
	vrx = VReadXLS(r'data\ea.sheets.xls')
	vrx.printSheet(0)

if __name__ == "__main__":
	main()

print r'Done. If next line is weird,' + \
	' something is wrong!'
print (u'если это видно, сбоев нет (%s)' % cp).encode(cp)
