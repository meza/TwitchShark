using System.Collections.Generic;
using static Twitch;

public class TwitchParser
{
    public TwitchCommand Parse(string raw)
    {
        Dictionary<string, string> tagDict = new Dictionary<string, string>();

        ParserState state = ParserState.STATE_NONE;
        int[] starts = new[] { 0, 0, 0, 0, 0, 0 };
        int[] lens = new[] { 0, 0, 0, 0, 0, 0 };
        for (int i = 0; i < raw.Length; ++i)
        {
            lens[(int)state] = i - starts[(int)state] - 1;
            if (state == ParserState.STATE_NONE && raw[i] == '@')
            {
                state = ParserState.STATE_V3;
                starts[(int)state] = ++i;

                int start = i;
                string key = null;
                for (; i < raw.Length; ++i)
                {
                    if (raw[i] == '=')
                    {
                        key = raw.Substring(start, i - start);
                        start = i + 1;
                    }
                    else if (raw[i] == ';')
                    {
                        if (key == null)
                            tagDict[raw.Substring(start, i - start)] = "1";
                        else
                            tagDict[key] = raw.Substring(start, i - start);
                        start = i + 1;
                    }
                    else if (raw[i] == ' ')
                    {
                        if (key == null)
                            tagDict[raw.Substring(start, i - start)] = "1";
                        else
                            tagDict[key] = raw.Substring(start, i - start);
                        break;
                    }
                }
            }
            else if (state < ParserState.STATE_PREFIX && raw[i] == ':')
            {
                state = ParserState.STATE_PREFIX;
                starts[(int)state] = ++i;
            }
            else if (state < ParserState.STATE_COMMAND)
            {
                state = ParserState.STATE_COMMAND;
                starts[(int)state] = i;
            }
            else if (state < ParserState.STATE_TRAILING && raw[i] == ':')
            {
                state = ParserState.STATE_TRAILING;
                starts[(int)state] = ++i;
                break;
            }
            else if (state < ParserState.STATE_TRAILING && raw[i] == '+' || state < ParserState.STATE_TRAILING && raw[i] == '-')
            {
                state = ParserState.STATE_TRAILING;
                starts[(int)state] = i;
                break;
            }
            else if (state == ParserState.STATE_COMMAND)
            {
                state = ParserState.STATE_PARAM;
                starts[(int)state] = i;
            }

            while (i < raw.Length && raw[i] != ' ')
                ++i;
        }

        lens[(int)state] = raw.Length - starts[(int)state];
        string cmd = raw.Substring(starts[(int)ParserState.STATE_COMMAND],
            lens[(int)ParserState.STATE_COMMAND]);

        string parameters = raw.Substring(starts[(int)ParserState.STATE_PARAM],
                lens[(int)ParserState.STATE_PARAM]);
        string message = raw.Substring(starts[(int)ParserState.STATE_TRAILING],
            lens[(int)ParserState.STATE_TRAILING]);
        string hostmask = raw.Substring(starts[(int)ParserState.STATE_PREFIX],
            lens[(int)ParserState.STATE_PREFIX]);

        return new TwitchCommand
        {
            Command = cmd,
            Message = message,
            Parameters = parameters,
            Hostmask = hostmask,
            Tags = tagDict
        };
    }

    private enum ParserState
    {
        STATE_NONE,
        STATE_V3,
        STATE_PREFIX,
        STATE_COMMAND,
        STATE_PARAM,
        STATE_TRAILING
    };
}