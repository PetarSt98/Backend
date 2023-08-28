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
# Authentication
username = sys.argv[3] if len(sys.argv) > 2 else exit("Please specify the username")
password = sys.argv[4] if len(sys.argv) > 3 else exit("Please specify the password")
admins_only_flag = sys.argv[5] if len(sys.argv) > 4 else exit("Please specify the adminsOnly flag")

if (admins_only_flag != 'false' and admins_only_flag != "true"):
        exit("Please specify the adminsOnly flag as string true or false")

token = client.service.getAuthToken(username, password, 'CERN')
authenticationHeader = Element('Auth').insert(Element('token').setText(token))
client.set_options(soapheaders=authenticationHeader)

egroups = None

if (admins_only_flag == 'false'):
        # Calling getDeviceInfo
        deviceName = sys.argv[2] if len(sys.argv) > 1 else exit("Please specify the set name")
        userName = sys.argv[1] if len(sys.argv) > 1 else exit("Please specify the userName")
        result = client.service.getDeviceInfo(deviceName)


        # Define ldapsearch command
        ldapsearch_base_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "OU=Users,OU=Organic Units,DC=cern,DC=ch" "(&(objectClass=user)(mail=%s))" cn | grep \'^cn: \' | sed \'s/^cn: //\''

        # Get user information by email from LDAP using ldapsearch
        owner_search_cmd = ldapsearch_base_cmd % result.ResponsiblePerson.Email
        owner_info_process = subprocess.Popen(owner_search_cmd, stdout=subprocess.PIPE, shell=True)
        owner_info = owner_info_process.communicate()[0].strip()

        user_search_cmd = ldapsearch_base_cmd % result.UserPerson.Email
        user_info_process = subprocess.Popen(user_search_cmd, stdout=subprocess.PIPE, shell=True)
        user_info = user_info_process.communicate()[0].strip()

        # pprint(result.ResponsiblePerson.Name)
        # pprint(result.UserPerson.FirstName)

        if ('E-GROUP' in result.UserPerson.FirstName):
                ldapsearch_groups_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "DC=cern,DC=ch" "(&(objectClass=group)(cn={0}))" member'.format(result.ResponsiblePerson.Name)
                group_members_process = subprocess.Popen(ldapsearch_groups_cmd, stdout=subprocess.PIPE, shell=True)
                group_members = group_members_process.communicate()[0].strip()
                #print(group_members)
                egroups = group_members
                for group_member in group_members.splitlines():
                        if (userName in group_member):
                                user_info = userName
        ldapsearch_user_name_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "OU=Users,OU=Organic Units,DC=cern,DC=ch" "(&(objectClass=user)(sAMAccountName=%s))" cn | grep \'^cn: \' | sed \'s/^cn: //\'' % userName

	# Get user's full name using ldapsearch
        user_name_process = subprocess.Popen(ldapsearch_user_name_cmd, stdout=subprocess.PIPE, shell=True)
        user_full_name = user_name_process.communicate()[0].strip()

        pprint(result.ResponsiblePerson.Name)
        pprint(result.UserPerson.FirstName)
        pprint(user_info)
        pprint(owner_info)
        pprint(user_full_name)

ldapsearch_groups_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "DC=cern,DC=ch" "(&(objectClass=group)(cn=NICE Local Administrators Managers))" member'

# dfsmigapp01

group_members_process = subprocess.Popen(ldapsearch_groups_cmd, stdout=subprocess.PIPE, shell=True)
group_members = group_members_process.communicate()[0].strip()

print(group_members)
print("-------------------------")
print(egroups)
# ldapsearch_groups_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "OU=Users,OU=Organic Units,DC=cern,DC=ch" "(&(objectClass=user)(cn=%s))" memberOf'

# user_groups_search_cmd = ldapsearch_groups_cmd % "support-windows-servers"
# user_groups_process = subprocess.Popen(user_groups_search_cmd, stdout=subprocess.PIPE, shell=True)
# user_groups = user_groups_process.communicate()[0].strip()

# print(user_groups)