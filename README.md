# UniMob [![Github license](https://img.shields.io/github/license/codewriter-packages/UniMob.svg?style=flat-square)](#) [![Unity 2019.3](https://img.shields.io/badge/Unity-2019.3+-2296F3.svg?style=flat-square)](#) ![GitHub package.json version](https://img.shields.io/github/package-json/v/codewriter-packages/UniMob?style=flat-square) [![openupm](https://img.shields.io/npm/v/com.codewriter.unimob?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.codewriter.unimob/)
_Modern reactive programming library for Unity_

## Influences

UniMob inspired by [MobX](https://github.com/mobxjs/mobx) and [$mol_atom](https://github.com/eigenmethod/mol/tree/master/atom) and adapts the principles of reactive programming for Unity.

## Motivation

Reactive programming is good for building application logic and user interface in particular. This approach is extremely popular on the web and currently spreading in native development ([Android](https://developer.android.com/jetpack/compose/state), [iOS](https://developer.apple.com/documentation/combine/observableobject)).

However, there is only one implementation of reactive programming for Unity: [UniRx](https://github.com/neuecc/UniRx). UniRx is a great solution when calculations are distributed over time (such as delays and network requests). However, for modeling business logic and user interface, the ability to dynamically combine multiple reactive streams is much more important. And here Rx becomes too complicated. Select, Merge, Combine and other operators are extremely difficult for complex scenarios.

UniMob takes a different approach to building reactive streams and aims to make combining reactive streams the same as writing regular code.

## A quick example

So what does code that uses UniMob look like?

```csharp
using UniMob;
using UnityEngine.UI;

public class SampleCounter : LifetimeMonoBehaviour
{
    public Text counterText;
    public Button incrementButton;

    // declare reactive property
    [Atom] private int Counter { get; set; }

    protected override void Start()
    {
        // increment Counter on button click
        incrementButton.onClick.AddListener(() => Counter += 1);
        
        // Update counterText when Counter changed until Lifetime terminated
        Atom.Reaction(Lifetime, () => counterText.text = "Tap count: " + Counter);
    }
}
```

## Introduction

UniMob is a library that makes state management simple and scalable by transparently applying functional reactive programming. The philosophy behind UniMob is very simple:

> _Anything that can be derived from the application state, should be derived. Automatically._

which includes the UI, data serialization, server communication, etc.

## Core concepts

UniMob has only a few core concepts.

### Observable state

UniMob adds observable capabilities to existing data. This can simply be done by annotating your class properties with the `[Atom]` attribute.

```csharp
using UniMob;

public class Todo : ILifetimeScope
{
    public Todo(Lifetime lifetime) { Lifetime = lifetime; }
    public Lifetime Lifetime { get; }

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

public class TodoList : ILifetimeScope
{
    public TodoList(Lifetime lifetime) { Lifetime = lifetime; }
    public Lifetime Lifetime { get; }

    [Atom] public Todo[] Todos { get; set; } = new Todo[0];
    [Atom] public int UnfinishedTodoCount => Todos.Count(todo => !todo.Finished);
}
```

UniMob will ensure that `UnfinishedTodoCount` is updated automatically when a todo is added or when one of the finished properties is modified. Computations like these resemble formulas in spreadsheet programs like MS Excel. They update automatically and only when required.

### Reactions

Reactions are similar to a computed value, but instead of producing a new value, a reaction produces a side effect for things like printing to the console, making network requests, updating the UniMob.UI widgets, etc. In short, reactions bridge reactive and imperative programming.

#### UniMob.UI widgets

If you are using [UniMob.UI](https://github.com/codewriter-packages/UniMob.UI), you can use observable state in your widgets. UniMob will make sure the interface are always re-rendered whenever needed. (See [TodoList sample](https://github.com/codewriter-packages/UniMob.UI-Samples/tree/main/SimpleTodoList) for more info)

```csharp
public class TodoListApp : UniMobUIApp
{
    private TodoList todoList = new TodoList();

    protected override Widget Build(BuildContext context)
    {
        // Render scrollable list with todos,
        // list will be automatically updated when todos changed
        return new ScrollList {
            Children = {
                todoList.Todos.Select(todo => BuildTodo(todo))
            }
        };
    }

    private Widget BuildTodo(Todo todo)
    {
        return new TodoWidget(todo) { Key = Key.Of(todo) };
    }
}
```

#### Custom reactions

Custom reactions can simply be created using the `Reaction` or `When` methods to fit your specific situations.

For example the following `Reaction` prints a log message each time the amount of `UnfinishedTodoCount` changes:

```csharp
Atom.Reaction(Lifetime, () => {
    Debug.Log("Tasks left: " + todoList.UnfinishedTodoCount);
});
```

### Lifetime

UniMob handles atom lifecycle with Lifetime concept. Each atom is scoped to it's LIfetime (App Lifetime, View Lifetime, etc.). When Lifetime become disposed all scoped atoms automatically deactivates too. This allow to get rid of manual lifetime management complexity (such as Subscription pattern).

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