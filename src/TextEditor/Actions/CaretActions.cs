
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using System;

using NetFocus.DataStructure.TextEditor.Document;

namespace NetFocus.DataStructure.TextEditor.Actions 
{
	public class CaretLeft : AbstractEditAction
	{
		public override void Execute(TextArea textArea)
		{
			Point position = textArea.Caret.Position;
			ArrayList foldings = textArea.Document.FoldingManager.GetFoldedFoldingsWithEnd(position.Y);
			FoldMarker justBeforeCaret = null;
			foreach (FoldMarker fm in foldings) {
				if (fm.EndColumn == position.X) {
					justBeforeCaret = fm;
					break; // the first folding found is the folding with the smallest Startposition
				}
			}
			
			if (justBeforeCaret != null) {
				position.Y = justBeforeCaret.StartLine;
				position.X = justBeforeCaret.StartColumn;
//				Console.WriteLine("position set to " + position);
			} else {
				if (position.X > 0) {
					--position.X;
				} else if (position.Y  > 0) {
					LineSegment lineAbove = textArea.Document.GetLineSegment(position.Y - 1);
					position = new Point(lineAbove.Length, position.Y - 1);
				}
			}
			textArea.Caret.Position = position;
			textArea.SetDesiredColumn();
		}
	}
	
	public class CaretRight : AbstractEditAction
	{
		public override void Execute(TextArea textArea)
		{
			LineSegment curLine = textArea.Document.GetLineSegment(textArea.Caret.Line);
			Point position = textArea.Caret.Position;
			ArrayList foldings = textArea.Document.FoldingManager.GetFoldedFoldingsWithStart(position.Y);
			FoldMarker justBehindCaret = null;
			foreach (FoldMarker fm in foldings) {
				if (fm.StartColumn == position.X) {
					justBehindCaret = fm;
					break;
				}
			}
			if (justBehindCaret != null) {
				position.Y = justBehindCaret.EndLine;
				position.X = justBehindCaret.EndColumn;
			} else { // no folding is interesting
				if (position.X < curLine.Length || textArea.TextEditorProperties.AllowCaretBeyondEOL) {
					++position.X;
				} else if (position.Y + 1 < textArea.Document.TotalNumberOfLines) {
					++position.Y;
					position.X = 0;
				}
			}
			textArea.Caret.Position = position;
			textArea.SetDesiredColumn();
		}
	}
	
	public class CaretUp : AbstractEditAction
	{
		public override void Execute(TextArea textArea)
		{
			Point position = textArea.Caret.Position;
			int lineNr = position.Y;
			int visualLine = textArea.Document.GetVisibleLine(lineNr);
			if (visualLine > 0) {
				int xpos = textArea.TextViewMargin.GetDrawingXPos(lineNr, position.X);
				Point pos = new Point(xpos,
				                      textArea.TextViewMargin.DrawingRectangle.Y + (visualLine - 1) * textArea.TextViewMargin.FontHeight - textArea.TextViewMargin.TextArea.VirtualTop.Y);
				textArea.Caret.Position = textArea.TextViewMargin.GetLogicalPosition(pos.X, pos.Y);
				textArea.SetCaretToDesiredColumn(textArea.Caret.Position.Y);
			}
		}
	}
	
	public class CaretDown : AbstractEditAction
	{
		public override void Execute(TextArea textArea)
		{
			Point position = textArea.Caret.Position;
			int lineNr = position.Y;
			int visualLine = textArea.Document.GetVisibleLine(lineNr);
			if (visualLine < textArea.Document.GetVisibleLine(textArea.Document.TotalNumberOfLines)) {
				int xpos = textArea.TextViewMargin.GetDrawingXPos(lineNr, position.X);
				Point pos = new Point(xpos,
				                      textArea.TextViewMargin.DrawingRectangle.Y + (visualLine + 1) * textArea.TextViewMargin.FontHeight - textArea.TextViewMargin.TextArea.VirtualTop.Y);
				textArea.Caret.Position = textArea.TextViewMargin.GetLogicalPosition(pos.X, pos.Y);
				textArea.SetCaretToDesiredColumn(textArea.Caret.Position.Y);
			}
		}
	}
	
	public class WordRight : CaretRight
	{
		public override void Execute(TextArea textArea)
		{
			LineSegment line   = textArea.Document.GetLineSegment(textArea.Caret.Position.Y);
			Point oldPos = textArea.Caret.Position;
			Point newPos;
			if (textArea.Caret.Column >= line.Length) {
				newPos = new Point(0, textArea.Caret.Line + 1);
			} else {
				int nextWordStart = TextUtilities.FindNextWordStart(textArea.Document, textArea.Caret.Offset);
				newPos = textArea.Document.OffsetToPosition(nextWordStart);
			}
			
			// handle fold markers
			ArrayList foldings = textArea.Document.FoldingManager.GetFoldingsFromPosition(newPos.Y, newPos.X);
			foreach (FoldMarker marker in foldings) {
				if (marker.IsFolded) {
					if (oldPos.X == marker.StartColumn && oldPos.Y == marker.StartLine) {
						newPos = new Point(marker.EndColumn, marker.EndLine);
					} else {
						newPos = new Point(marker.StartColumn, marker.StartLine);
					}
					break;
				}
			}
			
			textArea.Caret.Position = newPos;
			textArea.SetDesiredColumn();
		}
	}
	
	public class WordLeft : CaretLeft
	{
		public override void Execute(TextArea textArea)
		{
			Point oldPos = textArea.Caret.Position;
			if (textArea.Caret.Column == 0) {
				base.Execute(textArea);
			} else {
				LineSegment line   = textArea.Document.GetLineSegment(textArea.Caret.Position.Y);
				
				int prevWordStart = TextUtilities.FindPrevWordStart(textArea.Document, textArea.Caret.Offset);
				
				Point newPos = textArea.Document.OffsetToPosition(prevWordStart);
				
				// handle fold markers
				ArrayList foldings = textArea.Document.FoldingManager.GetFoldingsFromPosition(newPos.Y, newPos.X);
				foreach (FoldMarker marker in foldings) {
					if (marker.IsFolded) {
						if (oldPos.X == marker.EndColumn && oldPos.Y == marker.EndLine) {
							newPos = new Point(marker.StartColumn, marker.StartLine);
						} else {
							newPos = new Point(marker.EndColumn, marker.EndLine);
						}
						break;
					}
				}
				textArea.Caret.Position = newPos;
				textArea.SetDesiredColumn();
			}
			
			
		}
	}
	
	public class ScrollLineUp : AbstractEditAction
	{
		public override void Execute(TextArea textArea)
		{
			textArea.AutoClearSelection = false;
			
			textArea.MotherTextAreaControl.VScrollBar.Value = Math.Max(textArea.MotherTextAreaControl.VScrollBar.Minimum, textArea.VirtualTop.Y - textArea.TextViewMargin.FontHeight);
		}
	}
	
	public class ScrollLineDown : AbstractEditAction
	{
		public override void Execute(TextArea textArea)
		{
			textArea.AutoClearSelection = false;
			textArea.MotherTextAreaControl.VScrollBar.Value = Math.Min(textArea.MotherTextAreaControl.VScrollBar.Maximum, textArea.VirtualTop.Y + textArea.TextViewMargin.FontHeight);
		}
	}
}