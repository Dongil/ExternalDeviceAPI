namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Static identity of a camera model. Carried as metadata on every <see cref="ICameraService"/>.
    /// </summary>
    public sealed class CameraInfo
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Description { get; set; }

        public CameraInfo Clone()
        {
            return new CameraInfo
            {
                Brand = Brand,
                Model = Model,
                Description = Description
            };
        }

        public override string ToString()
        {
            return (Brand ?? string.Empty) + " " + (Model ?? string.Empty);
        }
    }
}
