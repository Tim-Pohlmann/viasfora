﻿using System;
using System.Linq;
using Winterdom.Viasfora.Rainbow;
using Winterdom.Viasfora.Util;

namespace Winterdom.Viasfora.Languages.BraceScanners {
  public class CSharpBraceScanner : IBraceScanner, IResumeControl {
    const int stText = 0;
    const int stString = 1;
    const int stChar = 2;
    const int stMultiLineComment = 4;
    const int stIString = 5;

    private int status = stText;
    private int nestingLevel = 0;
    private bool parsingExpression = false;
    private bool multiLine = false;

    public String BraceList {
      get { return "(){}[]"; }
    }

    public CSharpBraceScanner() {
    }

    public void Reset(int state) {
      this.status = state & 0xFF;
      this.parsingExpression = (state & 0x08000000) != 0;
      this.nestingLevel = (state & 0xFF0000) >> 24;
      this.multiLine = (state & 0x04000000) != 0;
    }

    public bool CanResume(CharPos brace) {
      return brace.State == stText;
    }

    public bool Extract(ITextChars tc, ref CharPos pos) {
      while ( !tc.AtEnd ) {
        switch ( this.status ) {
          case stString:
            if ( this.multiLine ) {
              ParseMultiLineString(tc);
            } else {
              ParseString(tc);
            }
            break;
          case stChar: ParseCharLiteral(tc); break;
          case stMultiLineComment: ParseMultiLineComment(tc); break;
          case stIString:
            if ( ParseInterpolatedString(tc, ref pos) )
              return true;
            break;
          default:
            if ( ParseText(tc, ref pos) )
              return true;
            break;
        }
      }
      return false;
    }

    private bool ParseText(ITextChars tc, ref CharPos pos) {
      while ( !tc.AtEnd ) {
        // multi-line comment
        if ( tc.Char() == '/' && tc.NChar() == '*' ) {
          this.status = stMultiLineComment;
          tc.Skip(2);
          this.ParseMultiLineComment(tc);
        } else if ( tc.Char() == '/' && tc.NChar() == '/' ) {
          tc.SkipRemainder();
        } else if ( tc.Char() == '@' && tc.NChar() == '"' ) {
          this.status = stString;
          this.multiLine = true;
          tc.Skip(2);
          this.ParseMultiLineString(tc);
        } else if ( tc.Char() == '$' && tc.NChar() == '"' ) {
          // Roslyn interpolated string
          this.parsingExpression = false;
          this.status = stIString;
          tc.Skip(2);
          return this.ParseInterpolatedString(tc, ref pos);
        } else if ( tc.Char() == '$' && tc.NChar() == '@' && tc.NNChar() == '"' ) {
          this.status = stIString;
          this.multiLine = true;
          this.parsingExpression = false;
          tc.Skip(3);
          return this.ParseInterpolatedString(tc, ref pos);
        } else if ( tc.Char() == '"' ) {
          this.status = stString;
          tc.Next();
          this.ParseString(tc);
        } else if ( tc.Char() == '\'' ) {
          this.status = stString;
          tc.Next();
          this.ParseCharLiteral(tc);
        } else if ( this.BraceList.IndexOf(tc.Char()) >= 0 ) {
          pos = new CharPos(tc.Char(), tc.AbsolutePosition, EncodedState());
          tc.Next();
          return true;
        } else {
          tc.Next();
        }
      }
      return false;
    }

    private void ParseCharLiteral(ITextChars tc) {
      while ( !tc.AtEnd ) {
        if ( tc.Char() == '\\' ) {
          // skip over escape sequences
          tc.Skip(2);
        } else if ( tc.Char() == '\'' ) {
          tc.Next();
          break;
        } else {
          tc.Next();
        }
      }
      this.status = stText;
    }

    private void ParseString(ITextChars tc) {
      while ( !tc.AtEnd ) {
        if ( tc.Char() == '\\' ) {
          // skip over escape sequences
          tc.Skip(2);
        } else if ( tc.Char() == '"' ) {
          tc.Next();
          break;
        } else {
          tc.Next();
        }
      }
      this.status = stText;
    }

    private void ParseMultiLineString(ITextChars tc) {
      while ( !tc.AtEnd ) {
        if ( tc.Char() == '"' && tc.NChar() == '"' ) {
          // means a single embedded double quote
          tc.Skip(2);
        } else if ( tc.Char() == '"' ) {
          tc.Next();
          this.status = stText;
          this.multiLine = false;
          return;
        } else {
          tc.Next();
        }
      }
    }

    private void ParseMultiLineComment(ITextChars tc) {
      while ( !tc.AtEnd ) {
        if ( tc.Char() == '*' && tc.NChar() == '/' ) {
          tc.Skip(2);
          this.status = stText;
          return;
        } else {
          tc.Next();
        }
      }
    }
    // C# 6.0 interpolated string support:
    // this is a hack. It will not handle all possible expressions
    // but will handle most basic stuff
    private bool ParseInterpolatedString(ITextChars tc, ref CharPos pos) {
      while ( !tc.AtEnd ) {
        if ( parsingExpression ) {
          //
          // we're inside an interpolated section
          //
          if ( tc.Char() == '"' ) {
            // opening string
            tc.Next();
            this.ParseString(tc);
            this.status = stIString;
          } else if ( tc.Char() == '\'' ) {
            tc.Next();
            ParseCharLiteral(tc);
            this.status = stIString;
          } else if ( tc.Char() == '}' ) {
            // reached the end
            this.nestingLevel--;
            if ( nestingLevel == 0 ) {
              this.parsingExpression = false;
            }
            pos = new CharPos(tc.Char(), tc.AbsolutePosition, EncodedState());
            tc.Next();
            return true;
          } else if ( BraceList.Contains(tc.Char()) ) {
            pos = new CharPos(tc.Char(), tc.AbsolutePosition, EncodedState());
            if ( tc.Char() == '{' )
              this.nestingLevel++;
            tc.Next();
            return true;
          } else {
            tc.Next();
          }
        } else {
          //
          // parsing the string part
          // if it's an at-string, don't look for escape sequences
          //
          if ( tc.Char() == '\\' && !this.multiLine ) {
            // skip over escape sequences
            tc.Skip(2);
          } else if ( tc.Char() == '{' && tc.NChar() == '{' ) {
            tc.Skip(2);
          } else if ( tc.Char() == '{' ) {
            this.parsingExpression = true;
            this.nestingLevel++;
            pos = new CharPos(tc.Char(), tc.AbsolutePosition, EncodedState());
            tc.Next();
            return true;
          } else if ( this.multiLine && tc.Char() == '"' && tc.NChar() == '"' ) {
            // single embedded double quote
            tc.Skip(2);
          } else if ( tc.Char() == '"' ) {
            // done parsing the interpolated string
            this.status = stText;
            this.multiLine = false;
            tc.Next();
            break;
          } else {
            tc.Next();
          }
        }
      }
      return false;
    }

    private int EncodedState() {
      int encoded = status;
      if ( parsingExpression )
        encoded |= 0x08000000;
      if ( multiLine )
        encoded |= 0x04000000;
      encoded |= (nestingLevel & 0xFF) << 24;
      return encoded;
    }
  }
}
