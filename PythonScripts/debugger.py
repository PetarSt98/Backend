import winrm
import sys
import os


if __name__ == '__main__':
        username = os.environ.get('USERNAME')
        password = os.environ.get('PASSWORD')
        computer_names = os.environ.get('GATEWAYS_DEBUG').split(',')

        group_name = sys.argv[1]

        for computer_name in computer_names:
                print('Gateway server', computer_name, ':')
                # Creating a secure session using the credentials
                session = winrm.Session('http://{}:5985/wsman'.format(computer_name),
                                auth=(username, password),
                                transport='ntlm')
                # PowerShell command to get local group members
                ps_script = "Get-LocalGroupMember -Group '{}'".format(group_name)

                # Executing the command on the remote machine
                result = session.run_ps(ps_script)

                if result.status_code == 0:
                        # print("Command executed successfully.")
                        print(result.std_out.decode())
                else:
                        # print("Failed to execute command.")
                        print(result.std_err.decode())