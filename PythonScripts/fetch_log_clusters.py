import requests
import xml.etree.ElementTree as ET
import argparse
import os
import json


parser = argparse.ArgumentParser()
parser.add_argument('--username', type=str, required=True)
parser.add_argument('--fetchOnlyPublicCluster', type=str, required=True)
args = parser.parse_args()

username = os.environ.get('LOG_OFF_ADMIN_USERNAME')
password = os.environ.get('LOG_OFF_ADMIN_PASSWORD')

#print(args.username, args.fetchOnlyPublicCluster, username, password)

# Set the URL and authentication credentials
url = 'https://terminalservicesws.web.cern.ch/TerminalServicesWS/TerminalServicesAdminWS.asmx/getTSTableWithLoginInfoForUser'

# Parameters for the GET request
params = {
    'username': args.username,
    'fecthOnlyPublicCluster': args.fetchOnlyPublicCluster
}

# Make the GET request with Basic Authentication
response = requests.get(url, params=params, auth=(username, password))

data = []

# Check if the request was successful
if response.status_code == 200:
    # Parse the XML response
    root = ET.fromstring(response.text)

    # Find all 'Table' elements
    tables = root.findall(".//Table")

    # Iterate through each 'Table' and print the desired fields
    for table in tables:
        clusterName = table.find('ClusterName').text if table.find('ClusterName') is not None else 'N/A'
        isCluster = table.find('IsCluster').text if table.find('IsCluster') is not None else 'N/A'
        isConnected = table.find('isConnected').text if table.find('isConnected') is not None else 'N/A'
        #print(clusterName, ' ', isCluster, ' ', isConnected)
        data.append({
            "ClusterName": clusterName,
            "IsCluster": isCluster,
            "isConnected": isConnected
        })

    with open('cacheData/log_me_off_clusters_{}_{}.json'.format(args.username, args.fetchOnlyPublicCluster), 'w') as f:
        json.dump(data, f, indent=4)

    print("Data written to output.json")

else:
    print('Request failed with status code:', response.status_code)
    print(response.text.encode('utf-8'))