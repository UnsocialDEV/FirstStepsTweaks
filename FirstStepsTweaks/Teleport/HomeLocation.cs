namespace FirstStepsTweaks.Teleport
{
    public sealed class HomeLocation
    {
        public HomeLocation()
        {
        }

        public HomeLocation(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public HomeLocation(double x, double y, double z, long createdOrder)
        {
            X = x;
            Y = y;
            Z = z;
            CreatedOrder = createdOrder;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public long CreatedOrder { get; set; }
    }
}
