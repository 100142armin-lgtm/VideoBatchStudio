using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VideoBatchMerger;

internal sealed class NaturalPathComparer : IComparer<string>
{
	private static readonly Regex Parts = new Regex("(\\d+)", RegexOptions.Compiled);

	public int Compare(string x, string y)
	{
		if (object.ReferenceEquals(x, y))
		{
			return 0;
		}
		if (x == null)
		{
			return -1;
		}
		if (y == null)
		{
			return 1;
		}
		string[] array = Parts.Split(x);
		string[] array2 = Parts.Split(y);
		int num = Math.Min(array.Length, array2.Length);
		for (int i = 0; i < num; i++)
		{
			bool flag = long.TryParse(array[i], out var result);
			bool flag2 = long.TryParse(array2[i], out var result2);
			int num2 = ((!flag || !flag2) ? StringComparer.CurrentCultureIgnoreCase.Compare(array[i], array2[i]) : result.CompareTo(result2));
			if (num2 != 0)
			{
				return num2;
			}
		}
		return array.Length.CompareTo(array2.Length);
	}
}
