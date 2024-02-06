import paramiko
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--username', type=str, required=True)
args = parser.parse_args()

windows_server_ip = '188.185.127.32'
windows_server_username = r''
windows_server_password = ''

# PowerShell command to execute
powershell_command = 'Get-RDUserSession -ConnectionBroker rdslic2016-01.cern.ch | Where-Object {{ $_.UserName -eq \'{0}\' }}'.format(args.username)
# Create an SSH client
ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

try:
    #transport = ssh.get_transport()
    #transport.set_log_channel(-1)
    # Connect to the Windows server
    ssh.connect(windows_server_ip, username=windows_server_username, password=windows_server_password)

    # Execute the PowerShell command

    stdin, stdout, stderr = ssh.exec_command('powershell.exe -Command "{0}"'.format(powershell_command))
    print(stdout.read().decode())
    print(stderr.read().decode())

except Exception as e:
    print(f"An error occurred: {str(e)}")

finally:
    # Close the SSH connection
    ssh.close()