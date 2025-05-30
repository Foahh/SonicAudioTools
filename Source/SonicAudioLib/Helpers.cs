namespace SonicAudioLib;

public static class Helpers
{
    public static long Align(long value, long alignment)
    {
        while (value % alignment != 0)
        {
            value++;
        }

        return value;
    }
}