﻿using System;
using System.Collections.Generic;
using MonoDevelop.Components;
using MonoDevelop.Core;

namespace MonoDevelop.ResXEditor
{
    public abstract class ResXEditorListViewContent : ResXEditorViewContent
    {
        Xwt.ListStore store;
        Xwt.ListView listView;

        readonly HashSet<string> names = new HashSet<string>();
        protected readonly Xwt.DataField<string> countField = new Xwt.DataField<string>();
        protected readonly Xwt.DataField<string> nameField = new Xwt.DataField<string>();
        protected readonly Xwt.DataField<string> valueField = new Xwt.DataField<string>();
        protected readonly Xwt.DataField<string> commentField = new Xwt.DataField<string>();
        protected readonly Xwt.DataField<string> typeField = new Xwt.DataField<string>();
        protected readonly Xwt.DataField<ResXNode> nodeField = new Xwt.DataField<ResXNode>();

        protected sealed override void OnInitialize(ResXData data)
        {
            store = OnCreateListStore();

            listView = new Xwt.ListView(store)
            {
                GridLinesVisible = Xwt.GridLines.Both,
                SelectionMode = Xwt.SelectionMode.Multiple,
            };
            listView.ButtonPressed += OnButtonPress;
            listView.Show();

            AddListViewColumns(listView.Columns);

            foreach (var node in data.Nodes)
            {
                names.Add(node.Name);
                if (SkipNode(node))
                    continue;

                var row = store.AddRow();
                OnAddValues(store, row, node);
            }

            AddPlaceholder();
        }

        protected sealed override Xwt.Widget CreateContent() => listView;

        protected abstract bool SkipNode(ResXNode node);
        protected virtual ResXNode GetPlaceholder() => null;
        protected virtual Xwt.ListStore OnCreateListStore() => new Xwt.ListStore(countField, nameField, valueField, commentField, typeField, nodeField);

        protected virtual void AddListViewColumns(Xwt.ListViewColumnCollection collection)
        {
            collection.Add(" ", new Xwt.TextCellView(countField));
            collection.Add("Name", MakeEditableTextCell(nameField));
            collection.Add("Value", MakeEditableTextCell(valueField));
            collection.Add("Comment", MakeEditableTextCell(commentField));
        }

        protected virtual void OnAddValues(Xwt.ListStore store, int row, ResXNode node)
        {
            store.SetValues(row,
                            nameField, node.Name,
                            valueField, Data.GetValue(node).ToString(),
                            commentField, node.Comment ?? string.Empty,
                            typeField, node.TypeName ?? string.Empty,
                            nodeField, node);
        }

        void AddPlaceholder()
        {
            var placeholder = GetPlaceholder();
            if (placeholder != null)
            {
                var row = store.AddRow();
                OnAddValues(store, row, placeholder);
                store.SetValue(row, countField, "*");
            }
        }

        protected Xwt.TextCellView MakeEditableTextCell(Xwt.IDataField field, bool ellipsize = false)
        {
            var etc = new Xwt.TextCellView(field)
            {
                Editable = true,
                Ellipsize = ellipsize ? Xwt.EllipsizeMode.End : Xwt.EllipsizeMode.None,
                TextField = field,
            };
            etc.TextChanged += TextChanged;
            return etc;
        }

        void OnButtonPress(object o, Xwt.ButtonEventArgs args)
        {
            if (args.Button != Xwt.PointerButton.Right)
                return;

            var selection = listView.SelectedRows;

            var menu = new ContextMenu();
            var mi = new ContextMenuItem("Remove Row");
            mi.Clicked += OnPopupMenuActivated;
            menu.Add(mi);

            // FIXME: Coordinates
            menu.Show(listView.ToGtkWidget(), (int)args.X, (int)args.Y);
        }

        void OnPopupMenuActivated(object o, ContextMenuItemClickedEventArgs args)
        {
            int removed = 0;
            foreach (var row in listView.SelectedRows)
            {
                var name = store.GetValue(row, nameField);
                if (row == store.RowCount - 1)
                {
                    store.SetValues(row,
                                    valueField, string.Empty,
                                    commentField, null);
                    continue;
                }

                store.RemoveRow(row - removed++);
            }
            // FIXME: Serialize
            //Data.WriteToFile();
            //FileService.NotifyFileChanged(Data.Path);
        }


        void TextChanged(object o, Xwt.WidgetEventArgs args)
        {
            var etc = (Xwt.TextCellView)o;

            var row = listView.CurrentEventRow;
            var name = store.GetValue(row, nameField);
            if (name == string.Empty)
            {
                if (store.GetValue(row, valueField) != string.Empty)
                    store.SetValue(row, countField, "!");
                else
                    store.SetValue(row, countField, "*");
            }
            else
            {
                store.SetValue(row, countField, string.Empty);
                AddPlaceholder();
            }

			// FIXME: Need Xwt with NewText in args.
			string newText = etc.Text; // args.NewText;
			var node = store.GetValue(row, nodeField);

            args.Handled = UpdateNodeModel(node, etc, newText);
            if (listView.CurrentEventRow == store.RowCount - 1)
            {
                if (name != string.Empty)
                    AddPlaceholder();
            }

            // TODO: Maybe only do it on user save?
            //listView.ColumnsAutosize();
            //Data.WriteToFile();
        }

        bool UpdateNodeModel(ResXNode node, Xwt.TextCellView etc, string newText)
        {
            if (etc.TextField == nameField)
            {
                // If we already have a key with that name, revert to the old text, otherwise remove it from the set.
                if (names.Contains(newText) || newText == string.Empty)
                    return true;

                names.Remove(etc.Text);
                names.Add(newText);
                node.Name = newText;
            }
            else if (etc.TextField == valueField)
            {
                try
                {
                    // Check FileRef support.
                    node.ObjectValue = Convert.ChangeType(newText, Data.GetValue(node).GetType());
                }
                catch
                {
                    return true;
                }
            }
            else if (etc.TextField == commentField)
            {
                node.Comment = newText;
            }
            return false;
        }
    }
}
