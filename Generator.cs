namespace parser;

public class Generator
{
    private string _dstname;
    private string _srcname;
    private string _bodyText;
    private List<List<LexerTypes.Token>> _tokens;

    private readonly List<string> _keywordsInSwift = new()
    {
        "var",
        "let",
        "func",
    };

    private Dictionary<string, string> _keywordsInCpp = new()
    {
        {"var", "const auto"},
        {"let", "auto"},
        {"func", "function"},
    };

    private List<string> _addsInCpp = new()
    {
        {"for"},
        {"while"},
        {"if"},
    };

    public Generator(List<LexerTypes.Token> tokens)
    {
        _dstname = "result.cpp";
        _srcname = "input.swift";
        _bodyText = "";

        _tokens = new List<List<LexerTypes.Token>>();
        int curLine = 0;
        var tmpTokensList = new List<LexerTypes.Token>();
        foreach (var token in tokens)
        {
            if (curLine != token.line - 1)
            {
                _tokens.Add(tmpTokensList);
                tmpTokensList = new List<LexerTypes.Token>();
            }

            tmpTokensList.Add(token);
            curLine = token.line - 1;
        }

        _tokens.Add(tmpTokensList);
    }

    bool GenerateBody(StreamReader stream)
    {
        string prevLine = "";
        int lineCounter = 0;
        int numberOfTabs = 1;

        while (!stream.EndOfStream)
        {
            string? tmp = stream.ReadLine();
            tmp += '\n';
            bool needSemicolon = true;

            if (prevLine != tmp)
            {
                if (tmp == "\n")
                    _bodyText += '\n';
                else
                {
                    _bodyText += string.Concat(Enumerable.Repeat("  ", numberOfTabs));
                    var roundWasOpen = false;
                    if (_addsInCpp.Contains(_tokens[lineCounter][0].value))
                    {
                        _bodyText += _tokens[lineCounter][0].value + " ( ";
                        roundWasOpen = true;
                        if (_tokens[lineCounter][0].value == "for")
                        {
                            needSemicolon = false;
                            numberOfTabs++;
                            _bodyText += "auto ";

                            string ident = "";
                            var i = 1;
                            while (i < _tokens[lineCounter].Count)
                            {
                                if (_tokens[lineCounter][i].value == "in" &&
                                    _tokens[lineCounter][i + 1].value.Contains("..."))
                                {
                                    var values = _tokens[lineCounter][i + 1].value.Split("...");
                                    _bodyText += "=" + values[0] + "; " +
                                                 ident + "<=" + values[1] + "; " +
                                                 ident + "++ ) {";
                                    i = _tokens[lineCounter].Count;
                                }
                                else if (_tokens[lineCounter][i].value == "in")
                                {
                                    _bodyText += ": " + _tokens[lineCounter][i + 1].value + " ) " +
                                                 _tokens[lineCounter][i + 2].value + ' ';
                                    i = _tokens[lineCounter].Count;
                                }
                                else
                                {
                                    ident = _tokens[lineCounter][i].value;
                                    _bodyText += _tokens[lineCounter][i].value;
                                }

                                i++;
                            }
                        }
                        else
                            for (var i = 1; i < _tokens[lineCounter].Count; i++)
                            {
                                if (_tokens[lineCounter][i].value == "{" && roundWasOpen)
                                {
                                    needSemicolon = false;
                                    numberOfTabs++;
                                    roundWasOpen = false;
                                    _bodyText += ") " + _tokens[lineCounter][i].value + ' ';
                                }
                                else
                                {
                                    _bodyText += _tokens[lineCounter][i].value + ' ';
                                }
                            }
                    }
                    else
                    {
                        if (_keywordsInSwift.Contains(_tokens[lineCounter][0].value))
                            _bodyText += _keywordsInCpp[_tokens[lineCounter][0].value] + ' ';
                        else
                        {
                            if (_tokens[lineCounter][0].value == "}")
                            {
                                needSemicolon = false;
                                numberOfTabs--;
                                _bodyText = _bodyText.Substring(0, _bodyText.Length - 2 * numberOfTabs);
                                _bodyText += _tokens[lineCounter][0].value;
                            }
                            else
                            {
                                _bodyText += _tokens[lineCounter][0].value + ' ';
                            }
                        }

                        if (_tokens[lineCounter][0].value == "func" && _tokens[lineCounter].Count > 6)
                        {
                            var i = 1;
                            while (i < _tokens[lineCounter].Count)
                            {
                                if (_tokens[lineCounter][i].value == "(")
                                {
                                    _bodyText += " ( " + _tokens[lineCounter][i + 3].value + " " +
                                                 _tokens[lineCounter][i + 1].value + " ) {";
                                    needSemicolon = false;
                                    numberOfTabs++;
                                    i = _tokens[lineCounter].Count;
                                }
                                else
                                {
                                    _bodyText += _tokens[lineCounter][i].value;
                                }

                                i++;
                            }
                        }
                        else
                        {
                            bool flag = false;
                            for (var i = 1; i < _tokens[lineCounter].Count && !flag; i++)
                            {
                                if (_tokens[lineCounter][i].value == "{")
                                {
                                    needSemicolon = false;
                                    numberOfTabs++;
                                    _bodyText += _tokens[lineCounter][i].value;
                                }
                                else if (_tokens[lineCounter][i].value == "=" &&
                                         _tokens[lineCounter].Count - i - 1 == 3 &&
                                         (
                                             _tokens[lineCounter][i + 2].status == "PLUS" ||
                                             _tokens[lineCounter][i + 2].status == "MINUS"
                                         ) &&
                                         _tokens[lineCounter][i + 1].status == "NUMBER" &&
                                         _tokens[lineCounter][i + 3].status == "NUMBER")
                                {
                                    int firstSumEl = int.Parse(_tokens[lineCounter][i + 1].value);
                                    int secSumEl = int.Parse(_tokens[lineCounter][i + 3].value);
                                    _bodyText += "= " + (firstSumEl + secSumEl).ToString() + ' ';
                                    flag = true;
                                }
                                else
                                    _bodyText += _tokens[lineCounter][i].value + ' ';
                            }
                        }
                    }

                    _bodyText += needSemicolon ? ";\n" : '\n';
                }
            }

            prevLine = tmp;
            if (tmp != "\n")
                lineCounter++;
        }

        return true;
    }

    void GenerateStartMain(StreamWriter s)
    {
        s.WriteLine("int main() {");
    }

    void GenerateEndMain(StreamWriter s)
    {
        s.WriteLine("return 0;");
        s.WriteLine("}");
    }

    public void Optimize()
    {
    }

    public void Generate()
    {
        string dirName = AppDomain.CurrentDomain.BaseDirectory;
        FileInfo fileInfo = new FileInfo(dirName);
        DirectoryInfo parentDir = fileInfo.Directory!.Parent!;
        string parentDstDirName = parentDir!.FullName.Remove(parentDir!.FullName.Length - 9, 9) + _dstname;
        string parentSrcDirName = parentDir.FullName.Remove(parentDir!.FullName.Length - 9, 9) + _srcname;

        StreamWriter sw = new StreamWriter(parentDstDirName);
        StreamReader sr = new StreamReader(parentSrcDirName);

        GenerateStartMain(sw);
        GenerateBody(sr);
        sw.WriteLine(_bodyText);
        GenerateEndMain(sw);

        sw.Close();
        sr.Close();

        Console.WriteLine("\nGenerated");
    }
}