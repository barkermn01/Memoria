﻿/*
 * [The "BSD licence"]
 * Copyright (c) 2005-2008 Terence Parr
 * All rights reserved.
 *
 * Conversion to C#:
 * Copyright (c) 2008-2009 Sam Harwell, Pixel Mine, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace Antlr.Runtime.Tree
{

    /** <summary>A node representing erroneous token range in token stream</summary> */
    [System.Serializable]
    public class CommonErrorNode : CommonTree
    {
        public IIntStream input;
        public IToken start;
        public IToken stop;
        public RecognitionException trappedException;

        public CommonErrorNode(ITokenStream input, IToken start, IToken stop,
                               RecognitionException e)
        {
            //System.out.println("start: "+start+", stop: "+stop);
            if (stop == null ||
                 (stop.TokenIndex < start.TokenIndex &&
                  stop.Type != TokenTypes.EndOfFile))
            {
                // sometimes resync does not consume a token (when LT(1) is
                // in follow set.  So, stop will be 1 to left to start. adjust.
                // Also handle case where start is the first token and no token
                // is consumed during recovery; LT(-1) will return null.
                stop = start;
            }
            this.input = input;
            this.start = start;
            this.stop = stop;
            this.trappedException = e;
        }

        #region Properties
        public override bool IsNil
        {
            get
            {
                return false;
            }
        }
        public override string Text
        {
            get
            {
                string badText = null;
                if (start is IToken)
                {
                    int i = ((IToken)start).TokenIndex;
                    int j = ((IToken)stop).TokenIndex;
                    if (((IToken)stop).Type == TokenTypes.EndOfFile)
                    {
                        j = ((ITokenStream)input).Count;
                    }
                    badText = ((ITokenStream)input).ToString(i, j);
                }
                else if (start is ITree)
                {
                    badText = ((ITreeNodeStream)input).ToString(start, stop);
                }
                else
                {
                    // people should subclass if they alter the tree type so this
                    // next one is for sure correct.
                    badText = "<unknown>";
                }
                return badText;
            }
            set
            {
            }
        }
        public override int Type
        {
            get
            {
                return TokenTypes.Invalid;
            }
            set
            {
            }
        }
        #endregion

        public override string ToString()
        {
            if (trappedException is MissingTokenException)
            {
                return "<missing type: " +
                       ((MissingTokenException)trappedException).MissingType +
                       ">";
            }
            else if (trappedException is UnwantedTokenException)
            {
                return "<extraneous: " +
                       ((UnwantedTokenException)trappedException).UnexpectedToken +
                       ", resync=" + Text + ">";
            }
            else if (trappedException is MismatchedTokenException)
            {
                return "<mismatched token: " + trappedException.Token + ", resync=" + Text + ">";
            }
            else if (trappedException is NoViableAltException)
            {
                return "<unexpected: " + trappedException.Token +
                       ", resync=" + Text + ">";
            }
            return "<error: " + Text + ">";
        }
    }
}
