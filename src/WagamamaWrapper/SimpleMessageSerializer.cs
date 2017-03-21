using System.Text;

namespace WagamamaWrapper
{
    public class SimpleMessageSerializer : ISerializer
    {
        public byte[] Serialize<T>(T value)
        {
            return Encoding.UTF8.GetBytes(value.ToString());
        }

        public T Deserialize<T>(byte[] bytes)
        {
            return default(T);
        }
    }
}