#!/usr/bin/python
import sys
import subprocess
from suds.client import Client
from suds.sax.element import Element
from suds.xsd.doctor import ImportDoctor, Import
from pprint import pprint

# Client setup
url = 'https://network.cern.ch/sc/soap/soap.fcgi?v=6&WSDL'
imp = Import('http://schemas.xmlsoap.org/soap/encoding/')
doc = ImportDoctor(imp)
client = Client(url, doctor=doc, cache=None)
print "======"
print sys.argv[1]
print sys.argv[2]
print sys.argv[3]
print "======="
# Authentication
username = sys.argv[2] if len(sys.argv) > 2 else exit("Please specify the username")
password = sys.argv[3] if len(sys.argv) > 3 else exit("Please specify the password")
token = client.service.getAuthToken(username, password, 'CERN')
authenticationHeader = Element('Auth').insert(Element('token').setText(token))
client.set_options(soapheaders=authenticationHeader)

# Calling getDeviceInfo
deviceName = sys.argv[1] if len(sys.argv) > 1 else exit("Please specify the set name")
result = client.service.getDeviceInfo(deviceName)
print result.ResponsiblePerson.Email
# Define ldapsearch command
ldapsearch_base_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "OU=Users,OU=Organic Units,DC=cern,DC=ch" "(&(objectClass=user)(mail=%s))" cn | grep \'^cn: \' | sed \'s/^cn: //\''

# Get user information by email from LDAP using ldapsearch
owner_search_cmd = ldapsearch_base_cmd % result.ResponsiblePerson.Email
owner_info_process = subprocess.Popen(owner_search_cmd, stdout=subprocess.PIPE, shell=True)
owner_info = owner_info_process.communicate()[0].strip()

user_search_cmd = ldapsearch_base_cmd % result.UserPerson.Email
user_info_process = subprocess.Popen(user_search_cmd, stdout=subprocess.PIPE, shell=True)
user_info = user_info_process.communicate()[0].strip()

output_string = user_info
output_string += '\n'
output_string += owner_info

pprint(output_string)