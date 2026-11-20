using System;
using System.Collections.Generic;
using System.IO;
using RAMGuardian.Common.Serialization;

namespace RAMGuardian.Security.Policies
{
    public class ServiceProtectionPolicy
    {
        private static readonly HashSet<string> ImmutableSecurityServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WinDefend", "WdNisSvc", "Sense", "SecurityHealthService", "mpssvc", "wscsvc", "SgrmBroker"
        };

        private static readonly HashSet<string> ImmutableSystemServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wuauserv", "UsoSvc", "RpcSs", "RpcEptMapper", "DcomLaunch", "PlugPlay",
            "EventLog", "LSM", "SamSs", "CryptSvc", "ProfSvc", "Schedule", "BrokerInfrastructure"
        };

        private static readonly HashSet<string> ImmutableNetworkServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WlanSvc", "dot3svc", "nlasvc", "netprofm", "Dhcp", "Dnscache", "LanmanWorkstation", "LanmanServer"
        };

        private static readonly HashSet<string> ImmutableHardwareServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AudioSrv", "AudioEndpointBuilder", "AsusAppService", "AsusSystemAnalysis",
            "AsusSystemDiagnosis", "AsusSoftwareManager", "cphs", "IntelCpHDCPSvc"
        };

        private static readonly HashSet<string> ImmutableAcademicServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SafeExamBrowserService"
        };

        private static readonly HashSet<string> ImmutableResourceManagers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SysMain", "WSearch"
        };

        private readonly HashSet<string> _userProtectedServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _customConfigPath;

        public ServiceProtectionPolicy(string customConfigPath = @"C:\ProgramData\RAM Guardian\protected-services.json")
        {
            _customConfigPath = customConfigPath;
            LoadUserProtectedList();
        }

        private void LoadUserProtectedList()
        {
            try
            {
                if (File.Exists(_customConfigPath))
                {
                    string json = File.ReadAllText(_customConfigPath);
                    var list = JsonHelper.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(list[i]))
                            {
                                _userProtectedServices.Add(list[i].Trim());
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public bool CanModifyService(string serviceName)
        {
            string reason;
            if (IsServiceProtected(serviceName, out reason))
            {
                return false;
            }

            // Default policy: Do not modify unknown services
            return false;
        }

        public bool IsServiceProtected(string serviceName, out string reason)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                reason = "Unknown service (Default-DENY: Protected)";
                return true;
            }

            string clean = serviceName.Trim();

            if (ImmutableSecurityServices.Contains(clean))
            {
                reason = "Microsoft Defender / Windows Security Service";
                return true;
            }

            if (ImmutableSystemServices.Contains(clean))
            {
                reason = "Core Windows System Service";
                return true;
            }

            if (ImmutableNetworkServices.Contains(clean))
            {
                reason = "Core Windows Networking & Wi-Fi Service";
                return true;
            }

            if (ImmutableHardwareServices.Contains(clean))
            {
                reason = "Hardware, Audio, or ASUS Power/Diagnosis Service";
                return true;
            }

            if (ImmutableAcademicServices.Contains(clean))
            {
                reason = "Safe Exam Browser Academic Testing Service";
                return true;
            }

            if (ImmutableResourceManagers.Contains(clean))
            {
                reason = "Windows Memory & Search Management (Protected from automatic disabling)";
                return true;
            }

            if (_userProtectedServices.Contains(clean))
            {
                reason = "User-defined Protected Service";
                return true;
            }

            reason = "Standard Service (Modification Not Permitted by Default Policy)";
            return true;
        }
    }
}
