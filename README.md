# UniMob [![Github license](https://img.shields.io/github/license/codewriter-packages/UniMob.svg?style=flat-square)](#) [![Unity 2019.3](https://img.shields.io/badge/Unity-2019.3+-2296F3.svg?style=flat-square)](#) ![GitHub package.json version](https://img.shields.io/github/package-json/v/codewriter-packages/UniMob?style=flat-square)
_Reactive state management for Unity_

## Introduction

UniMob is a library that makes state management simple and scalable by transparently applying functional reactive programming. The philosophy behind UniMob is very simple:

> _Anything that can be derived from the application state, should be derived. Automatically._

which includes the UI, data serialization, server communication, etc.

## A quick example

So what does code that uses UniMob look like?

```csharp
using UniMob;
using UnityEngine;
using UnityEngine.UI;

public class Counter : MonoBehaviour
{
    public Text counterText;
    public Button incrementButton;

    // declare observable property
    [Atom] private int Counter { get; set; }

    private void Start()
    {
        // increment Counter on button click
        incrementButton.onClick.AddListener(() => Counter += 1);
        
        // Update counterText when Counter changed
        Atom.Reaction(() => counterText.text = "Tap count: " + Counter);
    }
}
```

## Core concepts

UniMob has only a few core concepts.

### Observable state

UniMob adds observable capabilities to existing data. This can simply be done by annotating your class properties with the `[Atom]` attribute.

```csharp
using UniMob;

public class Todo
{
    [Atom] public string Title { get; set; } = "";
    [Atom] public bool Finished { get; set; } = false;
}
```

Using `[Atom]` is like turning a property of an object into a spreadsheet cell that when modified may cause other cells to automatically recalculate or trigger reactions. 

### Computed values

With UniMob you can define values that will be derived automatically when relevant data is modified.

```csharp
using UniMob;
using System.Linq;

public class TodoList
{
    [Atom] public Todo[] Todos { get; set; } = new Todo[0];
    [Atom] public int UnfinishedTodoCount => Todos.Count(todo => !todo.Finished);
}
```

UniMob will ensure that `UnfinishedTodoCount` is updated automatically when a todo is added or when one of the finished properties is modified. Computations like these resemble formulas in spreadsheet programs like MS Excel. They update automatically and only when required.

### Reactions

Reactions are similar to a computed value, but instead of producing a new value, a reaction produces a side effect for things like printing to the console, making network requests, updating the UniMob.UI widgets, etc. In short, reactions bridge reactive and imperative programming.

#### UniMob.UI widgets

If you are using [UniMob.UI](https://github.com/codewriter-packages/UniMob.UI), you can use observable state in your widgets. UniMob will make sure the interface are always re-rendered whenever needed.

```csharp
public class TodoListApp : UniMobUIApp
{
    private TodoList todoList = new TodoList();

    protected override Widget Build(BuildContext context)
    {
        return new Column
        {
            MainAxisSize = AxisSize.Max,
            CrossAxisSize = AxisSize.Max,
            Children =
            {
                todoList.Todos.Select(todo => BuildTodo(todo)),
                new UniMobText(WidgetSize.FixedHeight(60))
                {
                    Value = "Tasks left: " + todoList.UnfinishedTodoCount
                }
            }
        };
    }

    private Widget BuildTodo(Todo todo)
    {
        return new UniMobButton
        {
            OnClick = () => todo.Finished = !todo.Finished,
            Child = new UniMobText(WidgetSize.FixedHeight(60))
            {
                Value = " - " + todo.Title + ": " + (todo.Finished ? "Finished" : "Active")
            }
        };
    }
}
```

#### Custom reactions

Custom reactions can simply be created using the `Reaction` or `When` methods to fit your specific situations.

For example the following `Reaction` prints a log message each time the amount of `UnfinishedTodoCount` changes:

```csharp
Atom.Reaction(() =>
{
    Debug.Log($"Tasks left: " + UnfinishedTodoCount);
});
```

### What will UniMob react to?

Why does a new message get printed each time the UnfinishedTodoCount is changed? The answer is this rule of thumb:

> _UniMob reacts to any existing observable property that is read during the execution of a tracked function._

### Actions

UniMob is unopinionated about how user events should be handled.

-   This can be done with Tasks.
-   Or by processing events using UniRx.
-   Or by simply handling events in the most straightforward way possible.

In the end it all boils down to: Somehow the state should be updated.

After updating the state UniMob will take care of the rest in an efficient, glitch-free manner. So simple statements, like below, are enough to automatically update the user interface.

```csharp
todoList.Todos = todoList.Todos
    .Append(new Todo("Get Coffee"))
    .Append(new Todo("Write simpler code"))
    .ToArray();
todoList.Todos[0].Finished = true;
```

## How to Install
Minimal Unity Version is 2019.3.

Library distributed as git package ([How to install package from git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html))
<br>Git URL: `https://github.com/codewriter-packages/UniMob.git`

## License

UniMob is [MIT licensed](./LICENSE.md).

## Credits

UniMob inspired by [$mol_atom](https://github.com/eigenmethod/mol/tree/master/atom) and [MobX](https://github.com/mobxjs/mobx).
