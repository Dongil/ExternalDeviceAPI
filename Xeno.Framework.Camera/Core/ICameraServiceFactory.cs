using System;
using System.Collections.Generic;

namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Maps camera model names to <see cref="ICameraService"/> instances.
    /// Hosts can supply a custom factory to add proprietary models without modifying the library.
    /// </summary>
    public interface ICameraServiceFactory
    {
        IList<CameraInfo> GetAvailableModels();
        CameraCapabilities GetCapabilities(string model);
        ICameraService Create(string model);
    }

    /// <summary>Default factory: knows about Sony EVI-H100 (serial) and SRG-300H (IP).</summary>
    public sealed class CameraServiceFactory : ICameraServiceFactory
    {
        public IList<CameraInfo> GetAvailableModels()
        {
            return new List<CameraInfo>
            {
                new CameraInfo { Brand = "Sony",  Model = "EVI-H100",        Description = "Sony EVI-H100 (RS-232/RS-422 VISCA)" },
                new CameraInfo { Brand = "Sony",  Model = "SRG-300H",        Description = "Sony SRG-300H (VISCA over IP)" },
                new CameraInfo { Brand = "FR",    Model = "FR-H50SN",        Description = "FR-H50SN (VISCA over IP, OEM firmware)" },
                new CameraInfo { Brand = "Canon", Model = "CR-N300",         Description = "Canon CR-N300 (VISCA over IP)" },
                new CameraInfo { Brand = "Pelco", Model = "Pelco-D Generic", Description = "Generic Pelco-D PTZ (RS-232/422/485 + UDP/TCP)" }
            };
        }

        public CameraCapabilities GetCapabilities(string model)
        {
            switch (model)
            {
                case "EVI-H100":        return CameraCapabilities.EviH100();
                case "SRG-300H":        return CameraCapabilities.SrgIp();
                case "FR-H50SN":        return CameraCapabilities.FrH50Sn();
                case "CR-N300":         return CameraCapabilities.CanonCrN300();
                case "Pelco-D Generic": return CameraCapabilities.PelcoDGeneric();
                default: throw new NotSupportedException("Unknown camera model: " + model);
            }
        }

        public ICameraService Create(string model)
        {
            switch (model)
            {
                case "EVI-H100":
                    return new Services.EviH100CameraService();
                case "SRG-300H":
                    return new Services.ViscaIpCameraService(
                        new CameraInfo { Brand = "Sony", Model = "SRG-300H", Description = "Sony SRG-300H PTZ (VISCA over IP)" },
                        CameraCapabilities.SrgIp());
                case "FR-H50SN":
                    return new Services.ViscaIpCameraService(
                        new CameraInfo { Brand = "FR", Model = "FR-H50SN", Description = "FR-H50SN PTZ (VISCA over IP, OEM firmware)" },
                        CameraCapabilities.FrH50Sn());
                case "CR-N300":
                    return new Services.ViscaIpCameraService(
                        new CameraInfo { Brand = "Canon", Model = "CR-N300", Description = "Canon CR-N300 PTZ (VISCA over IP)" },
                        CameraCapabilities.CanonCrN300());
                case "Pelco-D Generic":
                    return new Services.PelcoDCameraService();
                default:
                    throw new NotSupportedException("Unknown camera model: " + model);
            }
        }
    }
}
