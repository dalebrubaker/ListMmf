namespace BruSoftware.ListMmf
{
    public static class StringExtensions
    {
        /// <summary>
        /// Thanks to https://stackoverflow.com/questions/6373315/how-to-replace-a-char-in-string-with-an-empty-character-in-c-net
        /// </summary>
        /// <param name="input"></param>
        /// <param name="charItem"></param>
        /// <returns></returns>
        public static string RemoveCharFromString(this string input, char charItem)
        {
            var indexOfChar = input.IndexOf(charItem);
            if (indexOfChar < 0)
            {
                return input;
            }
            return RemoveCharFromString(input.Remove(indexOfChar, 1), charItem);
        }
    }
}
