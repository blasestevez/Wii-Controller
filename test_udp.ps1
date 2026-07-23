$c=New-Object System.Net.Sockets.UdpClient
$c.Connect('127.0.0.1', 26760)
$b=[System.Text.Encoding]::ASCII.GetBytes('Test DSU')
$c.Send($b, $b.Length) | Out-Null
$ep=New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
$rb=$c.Receive([ref]$ep)
Write-Host "Echo recibido (Hex): $([BitConverter]::ToString($rb))"
$c.Close()
