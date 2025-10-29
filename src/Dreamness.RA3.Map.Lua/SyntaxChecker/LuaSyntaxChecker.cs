using System.Text;
using Antlr4.Runtime;

namespace Dreamness.RA3.Map.Lua.SyntaxChecker;

public class LuaSyntaxChecker
{
    /// <summary>
    /// 检查 Lua 4.0 代码的语法
    /// </summary>
    /// <param name="luaCode">Lua 4.0 代码字符串</param>
    /// <returns>检查结果，包含是否通过和错误信息</returns>
    public static async Task<SyntaxCheckResult> CheckSyntax(string luaCode)
    {
        if (string.IsNullOrWhiteSpace(luaCode))
        {
            return new SyntaxCheckResult
            {
                IsValid = true,
                ErrorMessage = ""
            };
        }

        try
        {
            var inputStream = new AntlrInputStream(luaCode);
            var lexer = new Lua4Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Lua4Parser(tokenStream);

            // 添加错误监听器（传入源代码以便标记错误位置）
            var errorListener = new SyntaxErrorListener(luaCode);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            // 解析
            var tree = parser.chunk();
            
            // 语义分析（传入源代码）
            var semanticErrors = SemanticAnalyzer.PerformSemanticAnalysis(tree, luaCode);
            
            // 检查是否有错误
            if (errorListener.HasErrors || semanticErrors.Count > 0)
            {
                var errorMessages = new StringBuilder();
                
                // 添加词法/语法错误（带标记）
                foreach (var errorInfo in errorListener.ErrorInfos)
                {
                    errorMessages.AppendLine(errorInfo.FormattedMessage);
                    errorMessages.AppendLine(); // 空行分隔
                }
                
                // 添加语义错误（带标记）
                foreach (var error in semanticErrors)
                {
                    errorMessages.AppendLine(error);
                    errorMessages.AppendLine(); // 空行分隔
                }
                
                return new SyntaxCheckResult
                {
                    IsValid = false,
                    ErrorMessage = errorMessages.ToString().Trim()
                };
            }
            
            return new SyntaxCheckResult
            {
                IsValid = true,
                ErrorMessage = ""
            };
        }
        catch (RecognitionException ex)
        {
            return new SyntaxCheckResult
            {
                IsValid = false,
                ErrorMessage = $"Parse error at line {ex.OffendingToken.Line}: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new SyntaxCheckResult
            {
                IsValid = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// 检查文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>检查结果</returns>
    public static async Task<SyntaxCheckResult> CheckFileSyntax(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new SyntaxCheckResult
            {
                IsValid = false,
                ErrorMessage = $"Error: File '{filePath}' not found"
            };
        }

        try
        {
            string luaCode = File.ReadAllText(filePath);
            var result = await CheckSyntax(luaCode);
            result.FilePath = filePath;
            return result;
        }
        catch (Exception ex)
        {
            return new SyntaxCheckResult
            {
                IsValid = false,
                ErrorMessage = $"Error reading file: {ex.Message}",
                FilePath = filePath
            };
        }
    }
}

/// <summary>
/// 语法检查结果
/// </summary>
public class SyntaxCheckResult
{
    /// <summary>
    /// 是否通过语法检查
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// 文件路径（如果检查的是文件）
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// 获取简化的错误信息（第一行）
    /// </summary>
    public string GetFirstError()
    {
        if (string.IsNullOrEmpty(ErrorMessage))
            return "";
        
        var firstLine = ErrorMessage.Split('\n')[0].Trim();
        return firstLine;
    }
    
    public override string ToString()
    {
        if (IsValid)
            return "✓ Syntax is valid";
        else
            return $"✗ Syntax errors found:\n{ErrorMessage}";
    }
}

/// <summary>
/// 内部错误监听器
/// </summary>
class SyntaxErrorListener : BaseErrorListener
{
    private readonly List<ErrorInfo> _errors = new List<ErrorInfo>();
    private readonly string _sourceCode;

    public SyntaxErrorListener(string sourceCode = "")
    {
        _sourceCode = sourceCode ?? "";
    }

    public bool HasErrors => _errors.Count > 0;
    
    public List<ErrorInfo> ErrorInfos => _errors;

    public List<string> Errors
    {
        get
        {
            var result = new List<string>();
            foreach (var error in _errors)
            {
                result.Add(error.FormattedMessage);
            }
            return result;
        }
    }

    public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        string tokenText = "";
        if (offendingSymbol != null && offendingSymbol.Type != -1) // -1 is EOF token
        {
            tokenText = offendingSymbol.Text ?? "";
        }
        
        var errorInfo = new ErrorInfo
        {
            Line = line,
            Column = charPositionInLine,
            Message = msg,
            Token = tokenText,
            SourceCode = _sourceCode
        };
        
        _errors.Add(errorInfo);
    }
}

/// <summary>
/// 错误信息结构
/// </summary>
class ErrorInfo
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = "";
    public string Token { get; set; } = "";
    public string SourceCode { get; set; } = "";
    
    // 从错误消息中提取的完整token（可能比单个token更长）
    public int EffectiveTokenLength
    {
        get
        {
            // 首先使用实际的token长度
            int length = string.IsNullOrEmpty(Token) || Token == "<EOF>" ? 1 : Token.Length;
            
            // 尝试从错误消息中提取更多信息
            if (Message.Contains("input '"))
            {
                int startIdx = Message.IndexOf("input '");
                if (startIdx >= 0)
                {
                    int tokenStart = startIdx + 7; // "input '"的长度
                    int tokenEnd = Message.IndexOf("'", tokenStart);
                    if (tokenEnd > tokenStart)
                    {
                        string extractedToken = Message.Substring(tokenStart, tokenEnd - tokenStart);
                        if (!string.IsNullOrEmpty(extractedToken) && extractedToken.Length > length)
                        {
                            // 使用从错误消息中提取的token长度（通常更准确）
                            length = extractedToken.Length;
                        }
                    }
                }
            }
            
            return length;
        }
    }
    
    // 调整后的列位置（可能需要前移）
    public int AdjustedColumn
    {
        get
        {
            int effectiveLength = EffectiveTokenLength;
            if (effectiveLength > 1)
            {
                // 如果token长度大于1，可能需要向前调整列位置
                // 但这里我们保持原位置，因为错误通常指向第一个意外字符
                return Column;
            }
            return Column;
        }
    }

    public string FormattedMessage
    {
        get
        {
            var sb = new StringBuilder();
            
            // 基本信息
            sb.AppendLine($"Line {Line}:{Column}: {Message}");
            
            // 如果有源代码，显示标记行
            if (!string.IsNullOrEmpty(SourceCode))
            {
                var lines = SourceCode.Split('\n');
                if (Line > 0 && Line <= lines.Length)
                {
                    var errorLine = lines[Line - 1];
                    var trimmedLine = errorLine.TrimEnd('\r');
                    
                    // 显示错误行代码
                    sb.AppendLine(trimmedLine);
                    
                    // 构建标记字符串
                    var markers = new StringBuilder();
                    
                    // 计算有效的token长度（可能从错误消息中提取）
                    int tokenLength = EffectiveTokenLength;
                    
                    // 计算需要标记的位置（考虑全角和半角字符）
                    int actualColumn = Column;
                    
                    // 如果token长度大于1，需要向前调整起始位置
                    // 因为错误消息中的token可能是 ",)" 而我们指向的是 ")"
                    int startColumn = actualColumn;
                    if (tokenLength > 1)
                    {
                        // 向前调整到token的开始位置
                        // token长度包括当前字符，所以需要减去1
                        startColumn = Math.Max(0, actualColumn - (tokenLength - 1));
                    }
                    
                    int markerPos = 0;
                    
                    // 计算到标记起始位置前的空格数
                    for (int i = 0; i < startColumn && i < trimmedLine.Length; i++)
                    {
                        char c = trimmedLine[i];
                        if (c == '\t')
                        {
                            // Tab处理：通常tab是4或8个空格
                            int tabSize = 4;
                            int spacesToNextTab = tabSize - (markerPos % tabSize);
                            for (int j = 0; j < spacesToNextTab; j++)
                            {
                                markers.Append(' ');
                            }
                            markerPos += spacesToNextTab;
                        }
                        else
                        {
                            markers.Append(' ');
                            markerPos++;
                        }
                    }
                    
                    // 如果标记位置超出了行的长度，使用行的末尾
                    if (markerPos >= trimmedLine.Length)
                    {
                        markerPos = trimmedLine.Length;
                        while (markers.Length < markerPos)
                        {
                            markers.Append(' ');
                        }
                    }
                    
                    // token长度已经在上面计算过了
                    // 限制token长度避免过长
                    if (tokenLength > 50) tokenLength = 50;
                    
                    // 确保不超过行尾的可用空间
                    int availableSpace = trimmedLine.Length - markerPos;
                    if (availableSpace > 0)
                    {
                        tokenLength = Math.Min(tokenLength, availableSpace);
                        
                        // 如果有多个字符，使用连字符标记整个错误区域，箭头指向末尾
                        if (tokenLength >= 2)
                        {
                            // 使用连字符标记整个错误区域
                            for (int i = 0; i < tokenLength - 1; i++)
                            {
                                markers.Append('-');
                            }
                            markers.Append('^');
                        }
                        else
                        {
                            // 单个字符，直接使用箭头
                            markers.Append('^');
                        }
                    }
                    else
                    {
                        // 超出行尾，直接添加箭头
                        markers.Append('^');
                    }
                    
                    sb.AppendLine(markers.ToString());
                }
            }
            
            return sb.ToString().TrimEnd();
        }
    }
}

/// <summary>
/// 语义分析器
/// </summary>
class SemanticAnalyzer
{
    /// <summary>
    /// 执行语义分析
    /// </summary>
    public static List<string> PerformSemanticAnalysis(Lua4Parser.ChunkContext tree, string sourceCode = "")
    {
        var errors = new List<string>();
        var visitor = new BreakCheckVisitor();
        visitor.VisitChunk(tree);
        
        foreach (var breakError in visitor.BreakErrors)
        {
            var errorInfo = new ErrorInfo
            {
                Line = breakError.Line,
                Column = breakError.Column,
                Message = breakError.Message,
                Token = "break",
                SourceCode = sourceCode
            };
            errors.Add(errorInfo.FormattedMessage);
        }
        
        return errors;
    }
}


class BreakCheckVisitor : Lua4BaseVisitor<object>
{
    private int loopDepth = 0;
    public List<BreakError> BreakErrors = new List<BreakError>();

    public class BreakError
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public override object VisitBreakStatement(Lua4Parser.BreakStatementContext context)
    {
        if (loopDepth == 0)
        {
            var line = context.Start.Line;
            var column = context.Start.Column;
            BreakErrors.Add(new BreakError 
            { 
                Line = line, 
                Column = column, 
                Message = "no loop to break" 
            });
        }
        return base.VisitBreakStatement(context);
    }

    public override object VisitBlock(Lua4Parser.BlockContext context)
    {
        return base.VisitBlock(context);
    }

    public override object VisitWhileLoop(Lua4Parser.WhileLoopContext context)
    {
        loopDepth++;
        var result = base.VisitWhileLoop(context);
        loopDepth--;
        return result;
    }

    public override object VisitRepeatLoop(Lua4Parser.RepeatLoopContext context)
    {
        loopDepth++;
        var result = base.VisitRepeatLoop(context);
        loopDepth--;
        return result;
    }

    public override object VisitForLoop(Lua4Parser.ForLoopContext context)
    {
        loopDepth++;
        var result = base.VisitForLoop(context);
        loopDepth--;
        return result;
    }

    public override object VisitForInLoop(Lua4Parser.ForInLoopContext context)
    {
        loopDepth++;
        var result = base.VisitForInLoop(context);
        loopDepth--;
        return result;
    }
}

