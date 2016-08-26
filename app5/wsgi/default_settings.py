#!/usr/bin/env python
# -*- mode: python; coding: utf-8 -*-
# (c) Valik mailto:vasnake@gmail.com

""" Flask web app default settings for Cartobonus

"""

KVSBASEURL = 'http://keyvalstor.algis.com:7379'
LOGFILENAME = r'''c:/Inetpub/wwwroot/Apps/app5/wsgi/servlets_controller.log'''
SECRET_KEY = "Ake9Ii4Ohghah0IeR5AiOu3Ohphoh0aiGheenei0"
LAYERSLIST_XML = 'mapservices.list.xml'
TMPL_LAYERSLIST_XML = 'mapservices.list.%s.xml'
SESSION_PROTECTION = 'strong'
CBTOOLS_CONFIG = "c:/Inetpub/wwwroot/Apps/app5/Config/Tools.xml"
