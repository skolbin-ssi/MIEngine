﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MICore
{
    public class TcpTransport : StreamTransport
    {
        private TcpClient _client;

        public TcpTransport()
        {
        }

        protected override string GetThreadName()
        {
            return "MI.TcpTransport";
        }

        public override void InitStreams(LaunchOptions options, out StreamReader reader, out StreamWriter writer)
        {
            TcpLaunchOptions tcpOptions = (TcpLaunchOptions)options;

            _client = new TcpClient();
            _client.ConnectAsync(tcpOptions.Hostname, tcpOptions.Port).Wait();

            if (tcpOptions.Secure)
            {
                RemoteCertificateValidationCallback callback;

                if (tcpOptions.ServerCertificateValidationCallback == null)
                {
                    //if no callback specified, accept any certificate
                    callback = delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                    {
                        return sslPolicyErrors == SslPolicyErrors.None;
                    };
                }
                else
                {
                    //else use the callback specified
                    callback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => tcpOptions.ServerCertificateValidationCallback(sender, certificate, chain, sslPolicyErrors);
                }

                var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                certStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                SslStream sslStream = new SslStream(
                    _client.GetStream(),
                    false /* leaveInnerStreamOpen */,
                    callback,
                    null /*UserCertificateSelectionCallback */
                    );
#if !XPLAT
                // Starting with .NET Framework 4.7, this method authenticates using None, which allows the operating system to choose the best protocol to use, and to block protocols that are not secure.
                sslStream.AuthenticateAsClientAsync(tcpOptions.Hostname, certStore.Certificates, System.Security.Authentication.SslProtocols.None, false /* checkCertificateRevocation */).Wait();

#else
                //  In .NET Framework 4.6 (and .NET Framework 4.5 with the latest security patches installed), the allowed TLS/SSL protocols versions are 1.2, 1.1, and 1.0 (unless you disable strong cryptography by editing the Windows Registry).
                sslStream.AuthenticateAsClientAsync(tcpOptions.Hostname, certStore.Certificates, System.Security.Authentication.SslProtocols.Tls12, false /* checkCertificateRevocation */).Wait();
#endif
                reader = new StreamReader(sslStream);
                writer = new StreamWriter(sslStream);
            }
            else
            {
                reader = new StreamReader(_client.GetStream());
                writer = new StreamWriter(_client.GetStream());
            }
        }

        public override int DebuggerPid
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override void Close()
        {
            base.Close();
            ((IDisposable)_client).Dispose();
        }

        public override int ExecuteSyncCommand(string commandDescription, string commandText, int timeout, out string output, out string error)
        {
            throw new NotImplementedException();
        }

        public override bool CanExecuteCommand()
        {
            return false;
        }
    }
}
