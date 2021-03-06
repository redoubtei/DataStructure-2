
using System;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Runtime.Remoting;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

using NetFocus.DataStructure.TextEditor.Document;


namespace NetFocus.DataStructure.TextEditor
{
	/// <summary>
	/// This class paints the textarea.
	/// </summary>
	public class TextViewMargin : AbstractMargin
	{
		int          fontHeight;
		Hashtable    charWitdh           = new Hashtable();
		StringFormat measureStringFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
		Highlight    highlight;//代表高亮度显示的括号.
		int          physicalColumn = 0;
		
		public Highlight Highlight {
			get {
				return highlight;
			}
			set {
				highlight = value;
			}
		}
		
		public override Cursor Cursor {
			get {
				return Cursors.IBeam;
			}
		}
		
		public int FirstPhysicalLine {
			get {
				return textArea.VirtualTop.Y / fontHeight;
			}
		}
		public int LineHeightRemainder {
			get {
				return textArea.VirtualTop.Y % fontHeight;
			}
		}
		public int FirstVisibleLine {
			get {
				return textArea.Document.GetFirstLogicalLine(textArea.VirtualTop.Y / fontHeight);
			}
			set {
				if (FirstVisibleLine != value) {
					textArea.VirtualTop = new Point(textArea.VirtualTop.X, textArea.Document.GetVisibleLine(value) * fontHeight);
					
				}
			}
		}
		
		public int VisibleLineDrawingRemainder {
			get {
				return textArea.VirtualTop.Y % fontHeight;
			}
		}
		
		public int FontHeight {
			get {
				return fontHeight;
			}
		}
		
		public int VisibleLineCount {
			get {
				return 1 + DrawingRectangle.Height / fontHeight;
			}
		}
		
		public int VisibleColumnCount {
			get {
				return (int)(DrawingRectangle.Width / GetWidth(' ')) - 1;
			}
		}
		
		public TextViewMargin(TextArea textArea) : base(textArea)
		{
			measureStringFormat.LineAlignment = StringAlignment.Near;
			measureStringFormat.FormatFlags   = StringFormatFlags.MeasureTrailingSpaces | 
			                                    StringFormatFlags.FitBlackBox | 
			                                    StringFormatFlags.NoWrap | 
			                                    StringFormatFlags.NoClip;
			
			OptionsChanged();
		}
		
		public void OptionsChanged()
		{
			this.fontHeight = TextEditorProperties.Font.Height;
			if (this.charWitdh != null) {
				this.charWitdh.Clear();
			}
		}
		

		#region Paint functions
		
		public override void OnPaint(Graphics g, Rectangle rect)
		{
			if (rect.Width <= 0 || rect.Height <= 0) {
				return;
			}
			
			if (fontHeight != TextEditorProperties.Font.Height) {
				OptionsChanged();
				base.TextArea.Refresh();
				return;
			}
			
			int horizontalDelta = (int)(textArea.VirtualTop.X * GetWidth(g, ' '));
			if (horizontalDelta > 0) {
				g.SetClip(this.DrawingRectangle);
			}
			
			for (int y = 0; y < (DrawingRectangle.Height + VisibleLineDrawingRemainder) / fontHeight + 1; ++y) {
				Rectangle lineRectangle = new Rectangle(DrawingRectangle.X - horizontalDelta,
				                                        DrawingRectangle.Top + y * fontHeight - VisibleLineDrawingRemainder,
				                                        DrawingRectangle.Width + horizontalDelta,
				                                        fontHeight);
				if (rect.IntersectsWith(lineRectangle)) {
					int fvl = textArea.Document.GetVisibleLine(FirstVisibleLine);
					int currentLine = textArea.Document.GetFirstLogicalLine(fvl + y);
					PaintDocumentLine(g, currentLine, lineRectangle);
				}
			}
			
			if (horizontalDelta > 0) {
				g.ResetClip();
			}
		}
		
		void PaintDocumentLine(Graphics g, int lineNumber, Rectangle lineRectangle)
		{
			Brush bgColorBrush = GetBgColorBrush(lineNumber);
			Brush backgroundBrush = textArea.Enabled ?  bgColorBrush : SystemBrushes.InactiveBorder;
			
			if (lineNumber >= textArea.Document.TotalNumberOfLines) {
				g.FillRectangle(backgroundBrush, lineRectangle);
				if (TextEditorProperties.ShowInvalidLines) {//判断是否要画无效的行。
					DrawInvalidLineMarker(g, lineRectangle.Left, lineRectangle.Top);
				}
				if (TextEditorProperties.ShowVerticalRuler) {//判断是否要画垂直标尺。
					DrawVerticalRuler(g, lineRectangle);
				}
				return;
			}
			
			float physicalXPos = lineRectangle.X;
			int column = 0;
			physicalColumn = 0;

			if (TextEditorProperties.EnableFolding) {
				while (true) {
					ArrayList starts = textArea.Document.FoldingManager.GetFoldedFoldingsWithStartAfterColumn(lineNumber, column - 1);
					if (starts == null || starts.Count <= 0) {
						if (lineNumber < textArea.Document.TotalNumberOfLines) {
							physicalXPos = PaintLinePart(g, lineNumber, column, textArea.Document.GetLineSegment(lineNumber).Length, lineRectangle, physicalXPos);
						}
						break;
					}
					// search the first starting folding
					FoldMarker firstFolding = (FoldMarker)starts[0];
					foreach (FoldMarker fm in starts) {
						if (fm.StartColumn < firstFolding.StartColumn) {
							firstFolding = fm;
						}
					}
					starts.Clear();
					
					physicalXPos = PaintLinePart(g, lineNumber, column, firstFolding.StartColumn, lineRectangle, physicalXPos);
					column     = firstFolding.EndColumn;
					lineNumber = firstFolding.EndLine;
					
					ColumnRange    selectionRange2 = textArea.SelectionManager.GetSelectionAtLine(lineNumber);
					bool drawSelected = ColumnRange.WholeColumn.Equals(selectionRange2) || firstFolding.StartColumn >= selectionRange2.StartColumn && firstFolding.EndColumn <= selectionRange2.EndColumn;
						
					physicalXPos = PaintFoldingText(g, lineNumber, physicalXPos, lineRectangle, firstFolding.FoldText, drawSelected);
				}
			} else {
				physicalXPos = PaintLinePart(g, lineNumber, 0, textArea.Document.GetLineSegment(lineNumber).Length, lineRectangle, physicalXPos);
			}
			
			if (lineNumber < textArea.Document.TotalNumberOfLines) {
				// Paint things after end of line
				ColumnRange    selectionRange = textArea.SelectionManager.GetSelectionAtLine(lineNumber);
				LineSegment    currentLine    = textArea.Document.GetLineSegment(lineNumber);
				HighlightColor selectionColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("Selection");
				
				float spaceWidth         = GetWidth(g, ' ');
				bool  selectionBeyondEOL = selectionRange.EndColumn > currentLine.Length || ColumnRange.WholeColumn.Equals(selectionRange);
				
				if (TextEditorProperties.ShowEOLMarker) {
					HighlightColor eolMarkerColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("EOLMarkers");
					physicalXPos += DrawEOLMarker(g, eolMarkerColor.Color, selectionBeyondEOL ? bgColorBrush : backgroundBrush, physicalXPos, lineRectangle.Y);
				} else {
					if (selectionBeyondEOL) {
						g.FillRectangle(BrushRegistry.GetBrush(selectionColor.BackgroundColor), new RectangleF(physicalXPos, lineRectangle.Y, spaceWidth, lineRectangle.Height));
						physicalXPos += spaceWidth;
					}
				}
				
				Brush fillBrush = selectionBeyondEOL && TextEditorProperties.AllowCaretBeyondEOL ? bgColorBrush : backgroundBrush; 
				g.FillRectangle(fillBrush, 
				                new RectangleF(physicalXPos, lineRectangle.Y, lineRectangle.Width - physicalXPos + lineRectangle.X, lineRectangle.Height));
			}
			if (TextEditorProperties.ShowVerticalRuler) {
				DrawVerticalRuler(g, lineRectangle);
			}
		}
		
		bool DrawLineMarkerAtLine(int lineNumber)
		{
			return lineNumber == base.textArea.Caret.Line && textArea.MotherTextAreaControl.TextEditorProperties.LineViewerStyle == LineViewerStyle.FullRow;
		}
		
		Brush GetBgColorBrush(int lineNumber)
		{
			if (DrawLineMarkerAtLine(lineNumber)) {//如果当前行是标记行(即要画充满整个TextArea宽度的一行)
				HighlightColor caretLine = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("CaretMarker");
				return BrushRegistry.GetBrush(caretLine.Color);
			}
			HighlightColor background = (HighlightColor)textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("Default");
			return BrushRegistry.GetBrush(background.BackgroundColor);
		}
		
		float PaintFoldingText(Graphics g, int lineNumber, float physicalXPos, Rectangle lineRectangle, string text, bool drawSelected)
		{
			// TODO: get font and color from the highlighting file
			HighlightColor      selectionColor  = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("Selection");
			Brush               bgColorBrush    = drawSelected ? BrushRegistry.GetBrush(selectionColor.BackgroundColor) : GetBgColorBrush(lineNumber);
			Brush               backgroundBrush = textArea.Enabled ? bgColorBrush : SystemBrushes.InactiveBorder;
			
			float wordWidth = g.MeasureString(text, textArea.Font, Int32.MaxValue, measureStringFormat).Width;
			RectangleF rect = new RectangleF(physicalXPos, lineRectangle.Y, wordWidth, lineRectangle.Height - 1);
			
			g.FillRectangle(backgroundBrush, rect);
			
			physicalColumn += text.Length;
			g.DrawString(text,
			             textArea.Font,
			             BrushRegistry.GetBrush(drawSelected ? selectionColor.Color : Color.Gray),
			             rect, 
			             measureStringFormat);
			g.DrawRectangle(BrushRegistry.GetPen(drawSelected ? Color.DarkGray : Color.Gray), rect.X, rect.Y, rect.Width, rect.Height);
			
			// Bugfix for the problem - of overdrawn right rectangle lines.
			float ceiling = (float)Math.Ceiling(physicalXPos + wordWidth);
			if (ceiling - (physicalXPos + wordWidth) < 0.5) {
				++ceiling;
			}
			return ceiling;
		}
		
		void DrawMarker(Graphics g, TextMarker marker, RectangleF drawingRect)
		{
			float drawYPos = drawingRect.Bottom - 1;
			switch (marker.TextMarkerType) {
				case TextMarkerType.Underlined:
					g.DrawLine(BrushRegistry.GetPen(marker.Color), drawingRect.X, drawYPos, drawingRect.Right, drawYPos);
					break;
				case TextMarkerType.WaveLine:
					int reminder = ((int)drawingRect.X) % 6;
					for (float i = drawingRect.X - reminder; i < drawingRect.Right + reminder; i+= 6) {
						g.DrawLine(BrushRegistry.GetPen(marker.Color), i,     drawYPos + 3 - 4, i + 3, drawYPos + 1 - 4);
						g.DrawLine(BrushRegistry.GetPen(marker.Color), i + 3, drawYPos + 1 - 4, i + 6, drawYPos + 3 - 4);
					}
					break;
				case TextMarkerType.SolidBlock:
					g.FillRectangle(BrushRegistry.GetBrush(marker.Color), drawingRect);
					break;
			}
		}
		
		/// <summary>
		/// Get the marker brush (for solid block markers) at a given position.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="length">The length.</param>
		/// <returns>The Brush or null when no marker was found.</returns>
		Brush GetMarkerBrushAt(int offset, int length)
		{
			ArrayList markers = Document.TextMarkerStrategy.GetMarkers(offset,  length);
			foreach (TextMarker marker in markers) {
				if (marker.TextMarkerType == TextMarkerType.SolidBlock) {
					return BrushRegistry.GetBrush(marker.Color);
				}
			}
			return null;
		}
		                       
		
		float PaintLinePart(Graphics g, int lineNumber, int startColumn, int endColumn, Rectangle lineRectangle, float physicalXPos)
		{
			bool  drawLineMarker  = DrawLineMarkerAtLine(lineNumber);
			Brush bgColorBrush    = GetBgColorBrush(lineNumber);
			Brush backgroundBrush = textArea.Enabled ? bgColorBrush : SystemBrushes.InactiveBorder;
			
			HighlightColor selectionColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("Selection");
			ColumnRange    selectionRange = textArea.SelectionManager.GetSelectionAtLine(lineNumber);
			HighlightColor tabMarkerColor   = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("TabMarkers");
			HighlightColor spaceMarkerColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("SpaceMarkers");
			
			float spaceWidth   = GetWidth(g, ' ');
			
			LineSegment currentLine    = textArea.Document.GetLineSegment(lineNumber);
			
			int logicalColumn  = startColumn;
			
			Brush selectionBackgroundBrush  = BrushRegistry.GetBrush(selectionColor.BackgroundColor);
			Brush unselectedBackgroundBrush = backgroundBrush;

			if (currentLine.Words != null) {
				int startword = 0;
				int wordOffset = 0;
				//首先查找第一个不是空格或Tab的单词.
				for (; startword < currentLine.Words.Count; ++startword) {
					if (wordOffset >= startColumn) {
						break;
					}
					TextWord currentWord = ((TextWord)currentLine.Words[startword]);
					if (currentWord.Type == TextWordType.Tab) {
						++wordOffset;
					} else if (currentWord.Type == TextWordType.Space) {
						++wordOffset;
					} else {
						wordOffset     += currentWord.Length;
					}
				}
				
				
				for (int i = startword; i < currentLine.Words.Count; ++i) {
					
					// 到行的末尾(即全部的单词都画完了),则退出for语句.
					if (logicalColumn >= endColumn) {
						break;
					}
					
					ArrayList markers = Document.TextMarkerStrategy.GetMarkers(currentLine.Offset + wordOffset);
					foreach (TextMarker marker in markers) {
						if (marker.TextMarkerType == TextMarkerType.SolidBlock) {
							unselectedBackgroundBrush = BrushRegistry.GetBrush(marker.Color);
							break;
						}
					}
					// Clear old marker arrary		
					
					
					// TODO: cut the word if startColumn or endColimn is in the word;
					// needed for foldings wich can start or end in the middle of a word
					TextWord currentWord = ((TextWord)currentLine.Words[i]);
					switch (currentWord.Type) {
						case TextWordType.Space:
							RectangleF spaceRectangle = new RectangleF(physicalXPos, lineRectangle.Y, (float)Math.Ceiling(spaceWidth), lineRectangle.Height);
							
							Brush spaceBackgroundBrush;
							
							if (ColumnRange.WholeColumn.Equals(selectionRange) || logicalColumn >= selectionRange.StartColumn && logicalColumn < selectionRange.EndColumn) {
								spaceBackgroundBrush = selectionBackgroundBrush;
							} else {
								Brush markerBrush = GetMarkerBrushAt(currentLine.Offset + logicalColumn,  1);
								if (!drawLineMarker && markerBrush != null) {
									spaceBackgroundBrush = markerBrush;
								} else if (!drawLineMarker && currentWord.HighlightColor != null && currentWord.HighlightColor.HasBackground) {
									spaceBackgroundBrush = BrushRegistry.GetBrush(currentWord.HighlightColor.BackgroundColor);
								} else {
									spaceBackgroundBrush = unselectedBackgroundBrush;
								}
							}
							g.FillRectangle(spaceBackgroundBrush, spaceRectangle);
							
							if (TextEditorProperties.ShowSpaces) {
								DrawSpaceMarker(g, spaceMarkerColor.Color, physicalXPos, lineRectangle.Y);
							}
							foreach (TextMarker marker in markers) {
								DrawMarker(g, marker, spaceRectangle);
							}
							
							physicalXPos += spaceWidth;
							
							++logicalColumn;
							++physicalColumn;
							break;
						
						case TextWordType.Tab:
							
							int oldPhysicalColumn = physicalColumn;
							physicalColumn += TextEditorProperties.TabIndent;
							physicalColumn = (physicalColumn / TextEditorProperties.TabIndent) * TextEditorProperties.TabIndent;
							float tabWidth = (physicalColumn - oldPhysicalColumn) * spaceWidth;
							RectangleF tabRectangle = new RectangleF(physicalXPos, lineRectangle.Y, (float)Math.Ceiling(tabWidth), lineRectangle.Height);
							
							if (ColumnRange.WholeColumn.Equals(selectionRange) || logicalColumn >= selectionRange.StartColumn && logicalColumn <= selectionRange.EndColumn - 1) {
								spaceBackgroundBrush = selectionBackgroundBrush;
							} else {
								Brush markerBrush = GetMarkerBrushAt(currentLine.Offset + logicalColumn, 1);
								if (!drawLineMarker && markerBrush != null) {
									spaceBackgroundBrush = markerBrush;
								} else if (!drawLineMarker && currentWord.HighlightColor != null && currentWord.HighlightColor.HasBackground) {
									spaceBackgroundBrush = BrushRegistry.GetBrush(currentWord.HighlightColor.BackgroundColor);
								} else {
									spaceBackgroundBrush = unselectedBackgroundBrush;
								}
							}
							g.FillRectangle(spaceBackgroundBrush, tabRectangle);
							
							if (TextEditorProperties.ShowTabs) {
								DrawTabMarker(g, tabMarkerColor.Color, physicalXPos, lineRectangle.Y);
							}
							
							foreach (TextMarker marker in markers) {
								DrawMarker(g, marker, tabRectangle);
							}
							
							physicalXPos += tabWidth;
							
							++logicalColumn;
							break;
						
						case TextWordType.Word:
							string word    = currentWord.Word;
							float  lastPos = physicalXPos;
							
							Brush bgMarkerBrush = GetMarkerBrushAt(currentLine.Offset + logicalColumn,  word.Length);
							Brush wordBackgroundBrush;
							if (!drawLineMarker && bgMarkerBrush != null) {
								wordBackgroundBrush = bgMarkerBrush;
							} else if (!drawLineMarker && currentWord.HighlightColor.HasBackground) {
								wordBackgroundBrush = BrushRegistry.GetBrush(currentWord.HighlightColor.BackgroundColor);
							} else {
								wordBackgroundBrush = unselectedBackgroundBrush;
							}
							
							
							if (ColumnRange.WholeColumn.Equals(selectionRange) || selectionRange.EndColumn - 1  >= word.Length + logicalColumn &&
							                                                      selectionRange.StartColumn <= logicalColumn) {
								physicalXPos += DrawDocumentWord(g,
								                                 word,
								                                 new PointF(physicalXPos, lineRectangle.Y),
								                                 currentWord.Font,
								                                 selectionColor.HasForgeground ? selectionColor.Color : currentWord.Color,
								                                 selectionBackgroundBrush);
							} else {
								if (ColumnRange.NoColumn.Equals(selectionRange)  /* || selectionRange.StartColumn > logicalColumn + word.Length || selectionRange.EndColumn  - 1 <= logicalColumn */) {
									physicalXPos += DrawDocumentWord(g,
									                                 word,
									                                 new PointF(physicalXPos, lineRectangle.Y),
									                                 currentWord.Font,
									                                 currentWord.Color,
									                                 wordBackgroundBrush);
								} else {
									int offset1 = Math.Min(word.Length, Math.Max(0, selectionRange.StartColumn - logicalColumn ));
									int offset2 = Math.Max(offset1, Math.Min(word.Length, selectionRange.EndColumn - logicalColumn));
									
									physicalXPos += DrawDocumentWord(g,
									                                 word.Substring(0, offset1),
									                                 new PointF(physicalXPos, lineRectangle.Y),
									                                 currentWord.Font,
									                                 currentWord.Color,
									                                 wordBackgroundBrush);
									
									physicalXPos += DrawDocumentWord(g,
									                                 word.Substring(offset1, offset2 - offset1),
									                                 new PointF(physicalXPos, lineRectangle.Y),
									                                 currentWord.Font,
									                                 selectionColor.HasForgeground ? selectionColor.Color : currentWord.Color,
									                                 selectionBackgroundBrush);
									
									physicalXPos += DrawDocumentWord(g,
									                                 word.Substring(offset2),
									                                 new PointF(physicalXPos, lineRectangle.Y),
									                                 currentWord.Font,
									                                 currentWord.Color,
									                                 wordBackgroundBrush);
								}
							}
//							if (markerBrush != null) {
//								markerBrush.Dispose();
//							}
							foreach (TextMarker marker in markers) {
								if (marker.TextMarkerType != TextMarkerType.SolidBlock) {
									DrawMarker(g, marker, new RectangleF(lastPos, lineRectangle.Y, (physicalXPos - lastPos), lineRectangle.Height));
								}
							}
							
							// draw bracket highlight
							if (highlight != null) {
								if (highlight.OpenBrace.Y == lineNumber && highlight.OpenBrace.X == logicalColumn ||
								    highlight.CloseBrace.Y == lineNumber && highlight.CloseBrace.X == logicalColumn) {
									DrawBracketHighlight(g, new Rectangle((int)lastPos, lineRectangle.Y, (int)(physicalXPos - lastPos) - 1, lineRectangle.Height - 1));
								}
							}
							physicalColumn += word.Length;
							logicalColumn += word.Length;
							break;
					}
					markers.Clear();
				}//for
			}//if
			
			return physicalXPos;
		}
		
		float DrawDocumentWord(Graphics g, string word, PointF position, Font font, Color foreColor, Brush backBrush)
		{
			if (word == null || word.Length == 0) {
				return 0f;
			}
			float wordWidth = g.MeasureString(word, font, 32768, measureStringFormat).Width;
			g.FillRectangle(backBrush,
			                new RectangleF(position.X, position.Y, (float)Math.Ceiling(wordWidth), FontHeight));
			
			g.DrawString(word,font,BrushRegistry.GetBrush(foreColor),position.X,position.Y, measureStringFormat);
			return wordWidth;
		}
		
		
		#endregion
		
		#region Conversion Functions
		
		public float GetWidth(char ch)
		{
			if (!charWitdh.ContainsKey(ch)) {
				using (Graphics g = textArea.CreateGraphics()) {
					return GetWidth(g, ch);
				}
			}
			return (float)charWitdh[ch];
		}
		
		public float GetWidth(string text)
		{
			float width = 0;
			for (int i = 0; i < text.Length; ++i) {
				width += GetWidth(text[i]);
			}
			return width;
		}
		
		public float GetWidth(Graphics g, char ch)
		{
			if (!charWitdh.ContainsKey(ch)) {
				charWitdh.Add(ch, g.MeasureString(ch.ToString(), TextEditorProperties.Font, 2000, measureStringFormat).Width);
			}
			return (float)charWitdh[ch];
		}
		
		public int GetVisualColumn(int logicalLine, int logicalColumn)
		{
			return GetVisualColumn(Document.GetLineSegment(logicalLine), logicalColumn);
		}
		public int GetVisualColumn(LineSegment line, int logicalColumn)
		{
			int tabIndent = Document.TextEditorProperties.TabIndent;
			int column    = 0;
			for (int i = 0; i < logicalColumn; ++i) {
				char ch;
				if (i >= line.Length) {
					ch = ' ';
				} else {
					ch = Document.GetCharAt(line.Offset + i);
				}
				
				switch (ch) {
					case '\t':
						int oldColumn = column;
						column += tabIndent;
						column = (column / tabIndent) * tabIndent;
						break;
					default:
						++column;
						break;
				}
			}
			return column;
		}
		
		/// <summary>
		/// returns line/column for a visual point position
		/// </summary>
		public Point GetLogicalPosition(int xPos, int yPos)
		{
			xPos += (int)(textArea.VirtualTop.X * GetWidth(' '));
			int clickedVisualLine = Math.Max(0, (yPos + this.textArea.VirtualTop.Y) / fontHeight);
			int logicalLine       = Document.GetFirstLogicalLine(clickedVisualLine);
			Point pos = GetLogicalColumn(logicalLine, xPos);
			return pos;
		}
		
		/// <summary>
		/// returns logical line number for a visual point
		/// </summary>
		public int GetLogicalLine(Point mousepos)
		{
			int physicalLine = FirstPhysicalLine + (int)(mousepos.Y / FontHeight);
			return Document.GetFirstLogicalLine(physicalLine);
		}
		
		public Point GetLogicalColumn(int firstLogicalLine, int xPos)
		{
//			Console.WriteLine("GetLogicalColumn: line = {0}, xPos = {1}", firstLogicalLine, xPos);
			float spaceWidth = GetWidth(' ');
			LineSegment line = firstLogicalLine < Document.TotalNumberOfLines ? Document.GetLineSegment(firstLogicalLine) : null;
			if (line == null) {
//				Console.WriteLine("LineSegment not found");
				return new Point((int)(xPos / spaceWidth), firstLogicalLine);
			}
			
			int lineNumber    = firstLogicalLine;
			int tabIndent     = Document.TextEditorProperties.TabIndent;
			int column        = 0;
			int logicalColumn = 0;
			float paintPos    = 0;
				
			ArrayList starts = textArea.Document.FoldingManager.GetFoldedFoldingsWithStart(lineNumber);
			while (true) {
				// save current paint position 
				float oldPaintPos = paintPos;
				
				// search for folding
				if (starts.Count > 0) {
//					Console.WriteLine("Foldings found");
					foreach (FoldMarker folding in starts) {
						if (folding.IsFolded && logicalColumn >= folding.StartColumn && (logicalColumn < folding.EndColumn || lineNumber != folding.EndLine)) {
							column       += folding.FoldText.Length;
							paintPos     += folding.FoldText.Length * spaceWidth;
							// special case when xPos is inside the fold marker
							if (xPos <= paintPos - (paintPos - oldPaintPos) / 2) {
								return new Point(logicalColumn, lineNumber);
							}
							logicalColumn = folding.EndColumn;
							if (lineNumber != folding.EndLine) {
								lineNumber    = folding.EndLine;
								line          = Document.GetLineSegment(lineNumber);
								starts        = textArea.Document.FoldingManager.GetFoldedFoldingsWithStart(lineNumber);
							}
							break;
						}
					}
				}
				
				// --> no folding, going on with the count
				char ch = logicalColumn >= line.Length ? ' ' : Document.GetCharAt(line.Offset + logicalColumn);
				switch (ch) {
					case '\t':
						int oldColumn = column;
						column += tabIndent;
 						column = (column / tabIndent) * tabIndent;
						paintPos += (column - oldColumn) * spaceWidth;
						break;
					default:
						paintPos += GetWidth(ch);
						++column;
						break;
				}
				
				// when the paint position is reached, give it back otherwise advance to the next char
				if (xPos <= paintPos - (paintPos - oldPaintPos) / 2) {
					return new Point(logicalColumn, lineNumber);
				}
				
				++logicalColumn;
			}
		}
		
		/// <summary>
		/// returns line/column for a visual point position
		/// </summary>
		public FoldMarker GetFoldMarkerFromPosition(int xPos, int yPos)
		{
			xPos += (int)(textArea.VirtualTop.X * GetWidth(' '));
			int clickedVisualLine = (yPos + this.textArea.VirtualTop.Y) / fontHeight;
			int logicalLine       = Document.GetFirstLogicalLine(clickedVisualLine);
			return GetFoldMarkerFromColumn(logicalLine, xPos);
		}
		
		FoldMarker GetFoldMarkerFromColumn(int firstLogicalLine, int xPos)
		{
			float spaceWidth = GetWidth(' ');
			LineSegment line = firstLogicalLine < Document.TotalNumberOfLines ? Document.GetLineSegment(firstLogicalLine) : null;
			if (line == null) {
				return null;
			}
			
			int lineNumber    = firstLogicalLine;
			int tabIndent     = Document.TextEditorProperties.TabIndent;
			int column        = 0;
			int logicalColumn = 0;
			float paintPos    = 0;
				
			ArrayList starts = textArea.Document.FoldingManager.GetFoldedFoldingsWithStart(lineNumber);
			while (true) {
				// save current paint position 
				float oldPaintPos = paintPos;
				
				// search for folding
				if (starts.Count > 0) {
					foreach (FoldMarker folding in starts) {
						if (folding.IsFolded && logicalColumn >= folding.StartColumn && (logicalColumn < folding.EndColumn || lineNumber != folding.EndLine)) {
							column       += folding.FoldText.Length;
							paintPos     += folding.FoldText.Length * spaceWidth;
							// special case when xPos is inside the fold marker
							if (xPos <= paintPos) {
								return folding;
							}
							logicalColumn = folding.EndColumn;
							if (lineNumber != folding.EndLine) {
								lineNumber    = folding.EndLine;
								line          = Document.GetLineSegment(lineNumber);
								starts        = textArea.Document.FoldingManager.GetFoldedFoldingsWithStart(lineNumber);
							}
							break;
						}
					}
				}
				
				// --> no folding, going on with the count
				char ch = logicalColumn >= line.Length ? ' ' : Document.GetCharAt(line.Offset + logicalColumn);
				switch (ch) {
					case '\t':
						int oldColumn = column;
						column += tabIndent;
 						column = (column / tabIndent) * tabIndent;
						paintPos += (column - oldColumn) * spaceWidth;
						break;
					default:
						paintPos += GetWidth(ch);
						++column;
						break;
				}
				
				// when the paint position is reached, give it back otherwise advance to the next char
				if (xPos <= paintPos - (paintPos - oldPaintPos) / 2) {
					return null;
				}
				
				++logicalColumn;
			}
		}
		
		float CountColumns(ref int column, int start, int end, int logicalLine)
		{
			float spaceWidth = GetWidth(' ');
			float drawingPos = 0;
			int tabIndent  = Document.TextEditorProperties.TabIndent;
			for (int j = start; j < end; ++j) {
				char ch;
				LineSegment line = Document.GetLineSegment(logicalLine);
				if (j >= line.Length) {
					ch = ' ';
				} else {
					ch = Document.GetCharAt(line.Offset + j);
				}
				switch (ch) {
					case '\t':
						int oldColumn = column;
						column += tabIndent;
						column = (column / tabIndent) * tabIndent;
						drawingPos += (column - oldColumn) * spaceWidth;
						break;
					default:
						++column;
						drawingPos += GetWidth(ch);
						break;
				}
			}
			return drawingPos;
		}
		
		public int GetDrawingXPos(int logicalLine, int logicalColumn)
		{
			float spaceWidth = GetWidth(' ');
			ArrayList foldings = Document.FoldingManager.GetTopLevelFoldedFoldings();
			int i;
			FoldMarker f = null;
			// search the last folding that's interresting
			for (i = foldings.Count - 1; i >= 0; --i) {
				f = (FoldMarker)foldings[i];
				if (f.StartLine < logicalLine || f.StartLine == logicalLine && f.StartColumn < logicalColumn) {
					break;
				}
			}
			int lastFolding  = 0;
			int firstFolding = 0;
			int column       = 0;
			int tabIndent    = Document.TextEditorProperties.TabIndent;
			float drawingPos;
			// if no folding is interresting
			if (f == null || !(f.StartLine < logicalLine || f.StartLine == logicalLine && f.StartColumn < logicalColumn)) {
				drawingPos = CountColumns(ref column, 0, logicalColumn, logicalLine);
				return (int)(drawingPos - textArea.VirtualTop.X * spaceWidth);
			}
			
			// if logicalLine/logicalColumn is in folding
			if (f.EndLine > logicalLine || f.EndLine == logicalLine && f.EndColumn > logicalColumn) {
				logicalColumn = f.StartColumn;
				logicalLine = f.StartLine;
				--i;
			}
			lastFolding = i;
			
			// search backwards until a new visible line is reched
			for (; i >= 0; --i) {
				f = (FoldMarker)foldings[i];
				if (f.EndLine < logicalLine) { // reached the begin of a new visible line
					break;
				}
			}
			firstFolding = i + 1;
			
			if (lastFolding < firstFolding) {
				drawingPos = CountColumns(ref column, 0, logicalColumn, logicalLine);
				return (int)(drawingPos - textArea.VirtualTop.X * spaceWidth);
			}
			
			int foldEnd      = 0;
			drawingPos = 0;
			for (i = firstFolding; i <= lastFolding; ++i) {
				f = (FoldMarker)foldings[i];
				drawingPos += CountColumns(ref column, foldEnd, f.StartColumn, f.StartLine);
				foldEnd = f.EndColumn;
				column += f.FoldText.Length;
				drawingPos += GetWidth(f.FoldText);
			}
			drawingPos += CountColumns(ref column, foldEnd, logicalColumn, logicalLine);
			
			return (int)(drawingPos - textArea.VirtualTop.X * spaceWidth);
		}
		
		
		#endregion
		
		#region DrawHelper functions
		
		void DrawBracketHighlight(Graphics g, Rectangle rect)
		{
			g.FillRectangle(BrushRegistry.GetBrush(Color.FromArgb(50, 0, 0, 255)), rect);
			g.DrawRectangle(Pens.Blue, rect);
		}
		
		void DrawInvalidLineMarker(Graphics g, float x, float y)
		{
			HighlightColor invalidLinesColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("InvalidLines");
			g.DrawString("~", invalidLinesColor.Font, BrushRegistry.GetBrush(invalidLinesColor.Color), x, y, measureStringFormat);
		}
		
		void DrawSpaceMarker(Graphics g, Color color, float x, float y)
		{
			HighlightColor spaceMarkerColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("SpaceMarkers");
			g.DrawString("\u00B7", spaceMarkerColor.Font, BrushRegistry.GetBrush(color), x, y, measureStringFormat);
		}
		
		void DrawTabMarker(Graphics g, Color color, float x, float y)
		{
			HighlightColor tabMarkerColor   = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("TabMarkers");
			g.DrawString("\u00BB", tabMarkerColor.Font, BrushRegistry.GetBrush(color), x, y, measureStringFormat);
		}
		
		float DrawEOLMarker(Graphics g, Color color, Brush backBrush, float x, float y)
		{
			float width = GetWidth(g, '\u00B6');
			g.FillRectangle(backBrush,
			                new RectangleF(x, y, width, fontHeight));
			
			HighlightColor eolMarkerColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("EOLMarkers");
			
			g.DrawString("\u00B6", eolMarkerColor.Font, BrushRegistry.GetBrush(color), x, y, measureStringFormat);
			return width;
		}
		
		void DrawVerticalRuler(Graphics g, Rectangle lineRectangle)
		{
			if (TextEditorProperties.VerticalRulerRow < textArea.VirtualTop.X) {
				return;
			}
			HighlightColor vRulerColor = textArea.Document.HighlightingStrategy.GetEnvironmentColorForName("VRuler");
			
			int xpos = (int)(DrawingRectangle.Left + GetWidth(g, ' ') * (TextEditorProperties.VerticalRulerRow - textArea.VirtualTop.X));
			g.DrawLine(BrushRegistry.GetPen(vRulerColor.Color),
			           xpos,
			           lineRectangle.Top,
			           xpos,
			           lineRectangle.Bottom);
		}
		
		
		#endregion
	}
}
