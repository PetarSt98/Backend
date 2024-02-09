import requests
import xml.etree.ElementTree as ET
import argparse
import os
import json


parser = argparse.ArgumentParser()
parser.add_argument('--username', type=str, required=True)
parser.add_argument('--servername', type=str, required=True)
args = parser.parse_args()

username = os.environ.get('LOG_OFF_ADMIN_USERNAME')
password = os.environ.get('LOG_OFF_ADMIN_PASSWORD')

#print(args.username, args.fetchOnlyPublicCluster, username, password)

# Set the URL and authentication credentials
url = 'https://terminalservicesws.web.cern.ch/TerminalServicesWS/TerminalServicesAdminWS.asmx/logMeOff'

# Parameters for the GET request
params = {
    'username': args.username,
    'servername': args.servername
}

# Make the GET request with Basic Authentication
response = requests.get(url, params=params, auth=(username, password))

print(response.status_code)

