#!/usr/bin/python
import sys
import os
import subprocess
from suds.client import Client
from suds.sax.element import Element
from suds.xsd.doctor import ImportDoctor, Import
from pprint import pprint
import re

def check_device_in_ad(device_name):
    ldapsearch_cmd = (
        'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" '
        '-b "DC=cern,DC=ch" "(&(objectClass=computer)(cn={}))"'.format(device_name)
    )
    process = subprocess.Popen(ldapsearch_cmd, stdout=subprocess.PIPE, shell=True)
    output, _ = process.communicate()
    if "dn:" in output.decode():
        return True
    else:
        return False


def ldapsearch_group_members(group_name):
    ldapsearch_cmd = (
        'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" '
        '-b "DC=cern,DC=ch" "(&(objectClass=group)(cn={}))" member'.format(group_name)
    )
    process = subprocess.Popen(ldapsearch_cmd, stdout=subprocess.PIPE, shell=True)
    output, _ = process.communicate()
    return output.decode()

def ldapsearch_user_groups(username):
    ldapsearch_cmd = (
        'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" '
        '-b "DC=cern,DC=ch" "(&(objectClass=group)(member=CN={},OU=Users,OU=Organic Units,DC=cern,DC=ch))" cn'.format(username)
    )
    # print("Running LDAP search with command:", ldapsearch_cmd)  # Debug: Print the command
    process = subprocess.Popen(ldapsearch_cmd, stdout=subprocess.PIPE, shell=True)
    output, _ = process.communicate()
    # print("Raw LDAP output:", output.decode())  # Debug: Print raw output

    group_names = ''
    # Process the output to extract group names
    for line in output.decode().splitlines():
        if line.startswith('cn:'):
            group_names += line.split(': ')[1]
    return group_names

def get_group_members(group_output):
    members = []
    for line in group_output.splitlines():
        if line.startswith("member:"):
            member = line.split(":", 1)[1].strip()
            members.append(member)
    return members

def expand_groups(members):
    all_members = set()
    for member in members:
        if "OU=e-groups" in member:
            group_name = re.search("CN=([^,]+),", member)
            if group_name:
                nested_group_members = ldapsearch_group_members(group_name.group(1))
                all_members.update(expand_groups(get_group_members(nested_group_members)))
        else:
            all_members.add(member)
    return all_members

def flatten_nested_egroups(members):
        while True:
                flattened_members = expand_groups(members)
                if (len(members) == len(flattened_members)):
                        return members
                members = flattened_members


# Client setup
url = 'https://network.cern.ch/sc/soap/soap.fcgi?v=6&WSDL'
imp = Import('http://schemas.xmlsoap.org/soap/encoding/')
doc = ImportDoctor(imp)
client = Client(url, doctor=doc, cache=None)
username = os.environ.get('USERNAME')
password = os.environ.get('PASSWORD')
allowed_domains_str = os.environ.get('ALLOWED_DOMAINS')
allowed_domains = allowed_domains_str .split(',')
e_group_non_primary = os.environ.get('NON_PRIMARY_EGROUP')
e_group_admins = os.environ.get('ADMINS_EGROUP')
e_group_admins_cluster = os.environ.get('CLUSTER_ADMINS')

admins_only_flag = sys.argv[3] if len(sys.argv) > 2 else exit("Please specify the adminsOnly flag")

if (admins_only_flag != 'false' and admins_only_flag != "true"):
        exit("Please specify the adminsOnly flag as string true or false")

token = client.service.getAuthToken(username, password, 'CERN')
authenticationHeader = Element('Auth').insert(Element('token').setText(token))
client.set_options(soapheaders=authenticationHeader)

egroups = set()

if (admins_only_flag == 'false'):
        # Calling getDeviceInfo
        deviceName = sys.argv[2] if len(sys.argv) > 1 else exit("Please specify the set name")
        userName = sys.argv[1] if len(sys.argv) > 1 else exit("Please specify the userName")


	# if not check_device_in_ad(deviceName):
    	# 	pprint("AD NOT OK")
	# 	exit(1)	


        result = client.service.getDeviceInfo(deviceName)

        # Define ldapsearch command
        ldapsearch_base_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "OU=Users,OU=Organic Units,DC=cern,DC=ch" "(&(objectClass=user)(mail=%s))" cn | grep \'^cn: \' | sed \'s/^cn: //\''

        if (result.ResponsiblePerson is not None):
                # Get user information by email from LDAP using ldapsearch
                owner_search_cmd = ldapsearch_base_cmd % result.ResponsiblePerson.Email
                owner_info_process = subprocess.Popen(owner_search_cmd, stdout=subprocess.PIPE, shell=True)
                owner_info = owner_info_process.communicate()[0].strip()

                if ('E-GROUP' in result.ResponsiblePerson.FirstName):
                        if (userName.lower() == result.ResponsiblePerson.Name.lower()):
                                owner_info = userName.lower()
                        initial_group_members = ldapsearch_group_members(result.ResponsiblePerson.Name)
                        members = get_group_members(initial_group_members)
                        group_members = expand_groups(members)
                        egroups = egroups | group_members
                        for group_member in group_members:
                                if (userName in group_member):
                                        owner_info = userName
                                        break
                                else:
                                        owner_info = result.ResponsiblePerson.Name.lower()
        else:
                owner_info = ''

        if (result.UserPerson is not None):
                user_search_cmd = ldapsearch_base_cmd % result.UserPerson.Email
                user_info_process = subprocess.Popen(user_search_cmd, stdout=subprocess.PIPE, shell=True)
                user_info = user_info_process.communicate()[0].strip()


        # pprint(result.ResponsiblePerson.Name)
        # pprint(result.UserPerson.FirstName)

                if ('E-GROUP' in result.UserPerson.FirstName):
                        if (userName.lower() == result.UserPerson.Name.lower()):
                                user_info = userName.lower()
                        initial_group_members = ldapsearch_group_members(result.UserPerson.Name)
                        members = get_group_members(initial_group_members)
                        group_members = expand_groups(members)
                        #print(group_members)
                        egroups = egroups | group_members
                        for group_member in group_members:
                                if (userName in group_member):
                                        user_info = userName
        else:
                user_info = ''
        ldapsearch_user_name_cmd = 'ldapsearch -z 0 -E pr=1000/noprompt -LLL -x -h "xldap.cern.ch" -b "OU=Users,OU=Organic Units,DC=cern,DC=ch" "(&(objectClass=user)(sAMAccountName=%s))" cn | grep \'^cn: \' | sed \'s/^cn: //\'' % userName

        # Get user's full name using ldapsearch
        user_name_process = subprocess.Popen(ldapsearch_user_name_cmd, stdout=subprocess.PIPE, shell=True)
        user_full_name = user_name_process.communicate()[0].strip()
        if (user_full_name is None):
                user_full_name = userName
        if (user_full_name == ''):
                user_full_name = userName
        pprint(result.ResponsiblePerson.Name if result.ResponsiblePerson is not None else '')
        pprint(result.UserPerson.FirstName if result.UserPerson is not None else '')
        pprint(user_info)
        pprint(owner_info)
        pprint(user_full_name)
        pprint('Primary' if 'cern-accounts-primary' in ldapsearch_user_groups(userName) else 'Secondary')
        if (result.Interfaces != None):
                not_ok_flag = True
                for interface in result.Interfaces:
                        if (interface.NetworkDomainName in allowed_domains):
                                pprint("OK")
                                not_ok_flag = False
                                break
                if (not_ok_flag):
                        pprint("NOT OK")
        else:
                pprint("OK")

initial_group_members = ldapsearch_group_members(e_group_admins)
members = get_group_members(initial_group_members)
all_members = flatten_nested_egroups(members)

for member in sorted(all_members):
    print(member)

initial_group_members = ldapsearch_group_members(e_group_admins_cluster)
members = get_group_members(initial_group_members)
all_members = flatten_nested_egroups(members)

for member in sorted(all_members):
    print(member)

print("-------------------------")

if (egroups is None or len(egroups) == 0):
        print('')
else:
        for member in sorted(egroups):
                print(member)
#print(egroups)

if (admins_only_flag == 'false'):
        print("-------------------------")
        initial_group_members = ldapsearch_group_members(e_group_non_primary)
        members = get_group_members(initial_group_members)
        all_members = flatten_nested_egroups(members)

        for member in sorted(all_members):
            print(member)




