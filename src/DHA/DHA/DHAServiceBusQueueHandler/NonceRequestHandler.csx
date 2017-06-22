﻿#r "Newtonsoft.Json"
#r "Microsoft.ServiceBus"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Xml"
#r "DMDataContract.dll"
#load "NonceTable.csx"
#load "Utilities.csx"

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Devices.Management.DMDataContract;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

public class NonceRequestHandler
{
    static Random Rnd = new Random();
    static TimeSpan MethodCallTimeOut = new TimeSpan(0, 0, 30);

    public NonceRequestHandler(CloudTable nonceTable, ServiceClient iotHubServiceClient, TraceWriter log)
    {
        _log = log;
        _nonceTable = new NonceCloudTable(nonceTable, log);
        _iotHubServiceClient = iotHubServiceClient;
    }

    public async Task ProcessAsync(BrokeredMessage dhaEventData)
    {
        var deviceId = dhaEventData.Properties["iothub-connection-device-id"].ToString();
        _log.Info($"deviceId: {deviceId}");

        var nonce = GenerateNonce();

        // store in database
        await _nonceTable.SetNonceForDeviceAsync(deviceId, nonce);

        // Invoke device method
        var param = new DeviceHealthAttestationDataContract.GetReportMethodParam() { Nonce = nonce };
        var json = JsonConvert.SerializeObject(param);
        var cloudToDeviceMethod = new CloudToDeviceMethod(DeviceHealthAttestationDataContract.GetReportMethodName, MethodCallTimeOut);
        cloudToDeviceMethod.SetPayloadJson(json);

        var result = await _iotHubServiceClient.InvokeDeviceMethodAsync(deviceId, cloudToDeviceMethod);
        _log.Info($"InvokeDeviceMethodAsync status: {result.Status}");
    }

    private static string GenerateNonce()
    {
        // The nonce is in hex format, with a minimum size of 8 bytes, 
        // and a maximum size of 32 bytes.  Each 32-bit integer is 8 bytes
        // in hex, so use 4 random 32-bit-integer as nonce.   
        var nonce = String.Empty;
        for (int i = 0; i < 4; i++)
        {
            nonce += Rnd.Next().ToString("X8");
        }
        return nonce;
    }

    private NonceCloudTable _nonceTable;
    private TraceWriter _log;
    private ServiceClient _iotHubServiceClient;
}


