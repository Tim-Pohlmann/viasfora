﻿using System;
using System.Collections.Generic;
using Winterdom.Viasfora.Rainbow;
using Winterdom.Viasfora.Util;

namespace Viasfora.Tests.BraceExtractors {
  public class BaseExtractorTests {
    protected IList<CharPos> Extract(IBraceExtractor extractor, string input, int start, int state, bool reset=true) {
      if ( reset ) extractor.Reset(0);
      ITextChars chars = new StringChars(input, start);
      IList<CharPos> list = new List<CharPos>();
      CharPos cp = CharPos.Empty;
      while ( !chars.EndOfLine ) {
        if ( extractor.Extract(chars, ref cp) )
          list.Add(cp);
      }
      return list;
    }
   protected IList<CharPos> ExtractWithLines(IBraceExtractor extractor, string input, int start, int state) {
      extractor.Reset(0);

      input = input.Substring(start);

      String[] lines = input.Split('\r', '\n');
      List<CharPos> result = new List<CharPos>();
      foreach ( String line in lines ) {
        ITextChars chars = new StringChars(line);
        CharPos cp = CharPos.Empty;
        while ( !chars.EndOfLine ) {
          if ( extractor.Extract(chars, ref cp) )
            result.Add(cp);
        }
      }
      return result;
    }
  }
}
