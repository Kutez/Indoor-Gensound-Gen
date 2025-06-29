using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System;

class Program
{
	[DllImport(@"add_ogg_comment.dll")]
	public static extern int amp_AddOggComment([MarshalAs(UnmanagedType.LPStr)]string path, float mindist, float maxdist, float basevol, uint sndtype, float maxaidist);

	public static void Main(string[] args)
	{
		Console.WriteLine(args);
		amp_AddOggComment(args[1],30,150,1,2149580800,100);
	}
}