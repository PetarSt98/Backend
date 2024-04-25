import winrm
import os
import xmltodict
import json
import sys
from multiprocessing import Pool

def fetch_xml_as_json(args):
    computer_name, username, password, file_path, rap_name = args
    try:
        # Create a session to the remote computer
        session = winrm.Session('http://{}:5985/wsman'.format(computer_name), auth=(username, password), transport='ntlm')
        # PowerShell command to read XML file
        ps_script = 'Get-Content -Path "{}"'.format(file_path)
        # Execute the command
        result = session.run_ps(ps_script)

        if result.status_code == 0:
            # Successfully executed command, process XML to JSON
            xml_data = result.std_out.decode()
            parsed_data = xmltodict.parse(xml_data)

            # Accessing the nested AzApplicationGroup elements correctly
            az_applications = parsed_data.get('AzAdminManager', {}).get('AzApplication')
            if not az_applications:
                return "Error: 'AzApplication' not found in XML data."

            names = []
            members = {}
            if isinstance(az_applications, list):
                for application in az_applications:
                    az_app_groups = application.get('AzApplicationGroup')
                    if az_app_groups:
                        if isinstance(az_app_groups, list):
                            for item in az_app_groups:
                                name = item.get('@Name')
                                member = item.get('Member')
                                if name and name.startswith('RAP_'):
                                    names.append(name)
                                    if member:
                                        members[name] = True
                                    else:
                                        members[name] = False
                        else:
                            name = az_app_groups.get('@Name')
                            member = item.get('Member')
                            if name and name.startswith('RAP_'):
                                names.append(name)
                                if member:
                                    members[name] = True
                                else:
                                    members[name] = False
            else:
                az_app_groups = az_applications.get('AzApplicationGroup')
                if az_app_groups:
                    if isinstance(az_app_groups, list):
                        for item in az_app_groups:
                            name = item.get('@Name')
                            member = item.get('Member')
                            if name and name.startswith('RAP_'):
                                names.append(name)
                                if member:
                                    members[name] = True
                                else:
                                    members[name] = False
                    else:
                        name = az_app_groups.get('@Name')
                        member = item.get('Member')
                        if name and name.startswith('RAP_'):
                            names.append(name)
                            if member:
                                members[name] = True
                            else:
                                members[name] = False
            if rap_name in names:
                if members[rap_name]:
                    return rap_name
                else:
                    return 'Corrupted'
            else:
                return ''

        else:
            # Error occurred during command execution
            return 'Failed to fetch XML file: {}'.format(result.std_err.decode())
    except Exception as e:
        return str(e)

def main():
    username = os.environ.get('USERNAME')
    password = os.environ.get('PASSWORD')
    computer_names = os.environ.get('GATEWAYS_DEBUG').split(',')
    rap_name = sys.argv[1]
    file_path = r"C:\Windows\System32\tsgateway\rap.xml"

    # Pool of workers
    pool = Pool(processes=len(computer_names))
    args = [(name, username, password, file_path, rap_name) for name in computer_names]
    results = pool.map(fetch_xml_as_json, args)

    for result, name in zip(results, computer_names):
        print('Gateway server {}:'.format(name))
        print(result)

if __name__ == '__main__':
    main()