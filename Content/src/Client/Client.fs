module Client

open Elmish
#if (bridge)
open Elmish.Bridge
#endif
open Elmish.React
#if (reaction)
open Elmish.Streams
open FSharp.Control
#endif
#if (layout == "fulma-admin" || layout == "fulma-cover" || layout == "fulma-hero" || layout == "fulma-landing" || layout == "fulma-login")
open Fable.FontAwesome
open Fable.FontAwesome.Free
#endif
open Fable.React
open Fable.React.Props
#if (!remoting)
open Fetch.Types
open Thoth.Fetch
#endif
#if (layout != "none")
open Fulma
#endif
open Thoth.Json

open Shared

// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model =
    {
        Counter: Counter option
#if (bridge)
        Clock: System.DateTime option
#endif
    }

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| Increment
| Decrement
| InitialCountLoaded of Counter
#if (bridge)
| Remote of ClientMsg
| Outgoing of ServerMsg
#endif

#if (deploy == "iis" && server != "suave")
module ServerPath =
    open System
    open Fable.Core

    /// when publishing to IIS, your application most likely runs inside a virtual path (i.e. localhost/SafeApp)
    /// every request made to the server will have to account for this virtual path
    /// so we get the virtual path from the location
    /// `virtualPath` of `http://localhost/SafeApp` -> `/SafeApp/`
    [<Emit("window.location.pathname")>]
    let virtualPath : string = jsNative

    /// takes path segments and combines them into a valid path
    let combine (paths: string list) =
        paths
        |> List.map (fun path -> List.ofArray (path.Split('/')))
        |> List.concat
        |> List.filter (fun segment -> not (segment.Contains(".")))
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> String.concat "/"
        |> sprintf "/%s"

    /// Normalized the path taking into account the virtual path of the server
    let normalize (path: string) = combine [virtualPath; path]
#endif


#if (remoting)
module Server =

    open Shared
    open Fable.Remoting.Client

    #if (deploy == "iis" && server != "suave")
    // normalize routes so that they work with IIS virtual path in production
    let normalizeRoutes typeName methodName =
        Route.builder typeName methodName
        |> ServerPath.normalize

    /// A proxy you can use to talk to server directly
    let api : ICounterApi =
      Remoting.createApi()
      |> Remoting.withRouteBuilder normalizeRoutes
      |> Remoting.buildProxy<ICounterApi>
    #else
    /// A proxy you can use to talk to server directly
    let api : ICounterApi =
      Remoting.createApi()
      |> Remoting.withRouteBuilder Route.builder
      |> Remoting.buildProxy<ICounterApi>
    #endif
let initialCounter = Server.api.initialCounter
#elseif (deploy == "iis" && server != "suave")
let initialCounter () = Fetch.fetchAs<Counter> (ServerPath.normalize "/api/init")
#else
let initialCounter () = Fetch.fetchAs<Counter> "/api/init"
#endif

#if reaction
// defines the initial state
let init () : Model =
    {
        Counter = None
#if (bridge)
        Clock = None
#endif
    }
#else
// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    let initialModel =
        {
            Counter = None
#if (bridge)
            Clock = None
#endif
        }
    let loadCountCmd =
#endif
#if (!reaction && remoting)
        Cmd.OfAsync.perform initialCounter () InitialCountLoaded
#endif
#if (!reaction && !remoting)
        Cmd.OfPromise.perform initialCounter () InitialCountLoaded
#endif
#if (!reaction)
    initialModel, loadCountCmd
#endif

#if (reaction && remoting)
let load = AsyncRx.ofAsync (initialCounter ())
#endif
#if (reaction && !remoting)
let load = AsyncRx.ofPromise (initialCounter ())
#endif

#if (reaction)
let loadCount =
    load
    |> AsyncRx.map InitialCountLoaded
    |> AsyncRx.toStream "loading"

let stream model msgs =
    match model.Counter with
    | None -> loadCount
    | _ -> msgs
#endif

#if (reaction)
// The update function computes the next state of the application based on the current state and the incoming events/messages
let update (msg : Msg) (currentModel : Model) : Model =
    match currentModel, msg with
    | { Counter = Some counter }, Increment ->
        { currentModel with Counter = Some { Value = counter.Value + 1 } }
    | { Counter = Some counter }, Decrement ->
        { currentModel with Counter = Some { Value = counter.Value - 1 } }
    | _, InitialCountLoaded initialCount ->
        { currentModel with Counter = Some initialCount }
#if (bridge)
    | _, Remote (GetTime time) ->
        { currentModel with Clock = Some time }
    | _, Outgoing msg ->
        Bridge.Send msg
        currentModel

#endif
    | _ -> currentModel
#else
// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match currentModel, msg with
    | { Counter = Some counter }, Increment ->
        let nextModel = { currentModel with Counter = Some { Value = counter.Value + 1 } }
        nextModel, Cmd.none
    | { Counter = Some counter }, Decrement ->
        let nextModel = { currentModel with Counter = Some { Value = counter.Value - 1 } }
        nextModel, Cmd.none
    | _, InitialCountLoaded initialCount->
        let nextModel = { currentModel with Counter = Some initialCount }
        nextModel, Cmd.none
#if (bridge)
    | _, Remote (GetTime time) ->
        let nextModel = { currentModel with Clock = Some time }
        nextModel, Cmd.none
    | _, Outgoing msg ->
        currentModel, Cmd.bridgeSend msg
#endif
    | _ -> currentModel, Cmd.none
#endif


let safeComponents =
    let components =
        span [ ]
           [
#if (server == "suave")
             a [ Href "http://suave.io" ] [ str "Suave" ]
             str ", "
#elseif (server == "giraffe")
             a [ Href "https://github.com/giraffe-fsharp/Giraffe" ] [ str "Giraffe" ]
             str ", "
#elseif (server == "saturn")
             a [ Href "https://saturnframework.github.io" ] [ str "Saturn" ]
             str ", "
#endif
             a [ Href "http://fable.io" ] [ str "Fable" ]
             str ", "
             a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
#if (layout != "none")
             str ", "
             a [ Href "https://fulma.github.io/Fulma" ] [ str "Fulma" ]
#endif
#if (layout == "fulma-admin" || layout == "fulma-cover" || layout == "fulma-hero" || layout == "fulma-landing" || layout == "fulma-login")
             str ", "
             a [ Href "https://dansup.github.io/bulma-templates/" ] [ str "Bulma\u00A0Templates" ]
#endif
#if (reaction)
             str ", "
             a [ Href "http://elmish-streams.rtfd.io/" ] [ str "Elmish.Streams" ]
#endif
#if (remoting)
             str ", "
             a [ Href "https://zaid-ajaj.github.io/Fable.Remoting/" ] [ str "Fable.Remoting" ]
#endif
#if (bridge)
             str ", "
             a [ Href "https://github.com/Nhowka/Elmish.Bridge" ] [ str "Elmish.Bridge" ]
#endif
           ]

    span [ ]
        [ strong [] [ str "SAFE Template" ]
          str " powered by: "
          components ]

let show = function
| { Counter = Some counter } -> string counter.Value
| { Counter = None   } -> "Loading..."

#if (bridge)
let showTime = function
| { Clock = Some time } -> time.ToString("HH:mm:ss")
| { Clock = None } -> "Loading..."
#endif

#if (layout == "none")
let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ h1 [] [ str "SAFE Template" ]
          p  [] [ str "The initial counter is fetched from server" ]
          p  [] [ str "Press buttons to manipulate counter:" ]
          button [ OnClick (fun _ -> dispatch Decrement) ] [ str "-" ]
          div [] [ str (show model) ]
          button [ OnClick (fun _ -> dispatch Increment) ] [ str "+" ]
#if (bridge)
          p  [] [ str "Press buttons to manipulate the clock:" ]
          button [ OnClick (fun _ -> dispatch (Outgoing Start)) ] [ str "Start" ]
          div [] [ str (showTime model) ]
          button [ OnClick (fun _ -> dispatch (Outgoing Pause)) ] [ str "Pause" ]
#endif

          safeComponents ]
#elseif (layout == "fulma-basic")
let button txt onClick =
    Button.button
        [ Button.IsFullWidth
          Button.Color IsPrimary
          Button.OnClick onClick ]
        [ str txt ]

let view (model : Model) (dispatch : Msg -> unit) =
    div []
        [ Navbar.navbar [ Navbar.Color IsPrimary ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ ]
                    [ str "SAFE Template" ] ] ]

          Container.container []
              [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h3 [] [ str ("Press buttons to manipulate counter: " + show model) ] ]
                Columns.columns []
                    [ Column.column [] [ button "-" (fun _ -> dispatch Decrement) ]
                      Column.column [] [ button "+" (fun _ -> dispatch Increment) ] ]
#if (bridge)
                Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h3 [] [ str ("Press buttons to control clock: " + showTime model) ] ]
                Columns.columns []
                    [ Column.column [] [ button "Start" (fun _ -> dispatch (Outgoing Start)) ]
                      Column.column [] [ button "Pause" (fun _ -> dispatch (Outgoing Pause)) ] ]
#endif
              ]

          Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]
#elseif (layout == "fulma-admin")
let navBrand =
    Navbar.navbar [ Navbar.Color IsWhite ]
        [ Container.container [ ]
            [ Navbar.Brand.div [ ]
                [ Navbar.Item.a [ Navbar.Item.CustomClass "brand-text" ]
                      [ str "SAFE Admin" ] ]
              Navbar.menu [ ]
                  [ Navbar.Start.div [ ]
                      [ Navbar.Item.a [ ]
                            [ str "Home" ]
                        Navbar.Item.a [ ]
                            [ str "Orders" ]
                        Navbar.Item.a [ ]
                            [ str "Payments" ]
                        Navbar.Item.a [ ]
                            [ str "Exceptions" ] ] ] ] ]

let menu =
    Menu.menu [ ]
        [ Menu.label [ ]
              [ str "General" ]
          Menu.list [ ]
              [ Menu.Item.a [ ]
                    [ str "Dashboard" ]
                Menu.Item.a [ ]
                    [ str "Customers" ] ]
          Menu.label [ ]
              [ str "Administration" ]
          Menu.list [ ]
              [ Menu.Item.a [ ]
                  [ str "Team Settings" ]
                li [ ]
                    [ a [ ]
                        [ str "Manage Your Team" ]
                      Menu.list [ ]
                          [ Menu.Item.a [ ]
                                [ str "Members" ]
                            Menu.Item.a [ ]
                                [ str "Plugins" ]
                            Menu.Item.a [ ]
                                [ str "Add a member" ] ] ]
                Menu.Item.a [ ]
                    [ str "Invitations" ]
                Menu.Item.a [ ]
                    [ str "Cloud Storage Environment Settings" ]
                Menu.Item.a [ ]
                    [ str "Authentication" ] ]
          Menu.label [ ]
              [ str "Transactions" ]
          Menu.list [ ]
              [ Menu.Item.a [ ]
                    [ str "Payments" ]
                Menu.Item.a [ ]
                    [ str "Transfers" ]
                Menu.Item.a [ ]
                    [ str "Balance" ] ] ]

let breadcrump =
    Breadcrumb.breadcrumb [ ]
        [ Breadcrumb.item [ ]
              [ a [ ] [ str "Bulma" ] ]
          Breadcrumb.item [ ]
              [ a [ ] [ str "Templates" ] ]
          Breadcrumb.item [ ]
              [ a [ ] [ str "Examples" ] ]
          Breadcrumb.item [ Breadcrumb.Item.IsActive true ]
              [ a [ ] [ str "Admin" ] ] ]

let hero =
    Hero.hero [ Hero.Color IsInfo
                Hero.CustomClass "welcome" ]
        [ Hero.body [ ]
            [ Container.container [ ]
                [ Heading.h1 [ ]
                      [ str "Hello, Admin." ]
                  safeComponents ] ] ]

let info =
    section [ Class "info-tiles" ]
        [ Tile.ancestor [ Tile.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
            [ Tile.parent [ ]
                  [ Tile.child [ ]
                      [ Box.box' [ ]
                          [ Heading.p [ ]
                                [ str "439k" ]
                            Heading.p [ Heading.IsSubtitle ]
                                [ str "Users" ] ] ] ]
              Tile.parent [ ]
                  [ Tile.child [ ]
                      [ Box.box' [ ]
                          [ Heading.p [ ]
                                [ str "59k" ]
                            Heading.p [ Heading.IsSubtitle ]
                                [ str "Products" ] ] ] ]
              Tile.parent [ ]
                  [ Tile.child [ ]
                      [ Box.box' [ ]
                          [ Heading.p [ ]
                                [ str "3.4k" ]
                            Heading.p [ Heading.IsSubtitle ]
                                [ str "Open Orders" ] ] ] ]
              Tile.parent [ ]
                  [ Tile.child [ ]
                      [ Box.box' [ ]
                          [ Heading.p [ ]
                                [ str "19" ]
                            Heading.p [ Heading.IsSubtitle ]
                                [ str "Exceptions" ] ] ] ] ] ]

let counter (model : Model) (dispatch : Msg -> unit) =
    Field.div [ Field.IsGrouped ]
        [ Control.p [ Control.IsExpanded ]
            [ Input.text
                [ Input.Disabled true
                  Input.Value (show model) ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch Increment) ]
                [ str "+" ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch Decrement) ]
                [ str "-" ] ] ]

#if (bridge)
let clock (model : Model) (dispatch : Msg -> unit) =
    Field.div [ Field.IsGrouped ]
        [ Control.p [ Control.IsExpanded ]
            [ Input.text
                [ Input.Disabled true
                  Input.Value (showTime model) ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch (Outgoing Start)) ]
                [ str "Start" ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch (Outgoing Pause)) ]
                [ str "Pause" ] ] ]
#endif


let columns (model : Model) (dispatch : Msg -> unit) =
    Columns.columns [ ]
        [ Column.column [ Column.Width (Screen.All, Column.Is6) ]
            [ Card.card [ CustomClass "events-card" ]
                [ Card.header [ ]
                    [ Card.Header.title [ ]
                        [ str "Events" ]
                      Card.Header.icon [ ]
                          [ Icon.icon [ ]
                              [ Fa.i [ Fa.Solid.AngleDown ] [] ] ] ]
                  div [ Class "card-table" ]
                      [ Content.content [ ]
                          [ Table.table
                              [ Table.IsFullWidth
                                Table.IsStriped ]
                              [ tbody [ ]
                                  [ for _ in 1..10 ->
                                      tr [ ]
                                          [ td [ Style [ Width "5%" ] ]
                                              [ Icon.icon
                                                  [ ]
                                                  [ Fa.i [ Fa.Regular.Bell ] [] ] ]
                                            td [ ]
                                                [ str "Lorem ipsum dolor aire" ]
                                            td [ ]
                                                [ Button.a
                                                    [ Button.Size IsSmall
                                                      Button.Color IsPrimary ]
                                                    [ str "Action" ] ] ] ] ] ] ]
                  Card.footer [ ]
                      [ Card.Footer.div [ ]
                          [ str "View All" ] ] ] ]
          Column.column [ Column.Width (Screen.All, Column.Is6) ]
              [ Card.card [ ]
                  [ Card.header [ ]
                      [ Card.Header.title [ ]
                          [ str "Inventory Search" ]
                        Card.Header.icon [ ]
                            [ Icon.icon [ ]
                                [ Fa.i [Fa.Solid.AngleDown] [] ] ] ]
                    Card.content [ ]
                        [ Content.content [ ]
                            [ Control.div
                                [ Control.HasIconLeft
                                  Control.HasIconRight ]
                                [ Input.text
                                      [ Input.Size IsLarge ]
                                  Icon.icon
                                      [ Icon.Size IsMedium
                                        Icon.IsLeft ]
                                      [ Fa.i [Fa.Solid.Search] [] ]
                                  Icon.icon
                                      [ Icon.Size IsMedium
                                        Icon.IsRight ]
                                      [ Fa.i [Fa.Solid.Check] [] ] ] ] ] ]
                Card.card [ ]
                    [ Card.header [ ]
                        [ Card.Header.title [ ]
                              [ str "Counter" ]
                          Card.Header.icon [ ]
                              [ Icon.icon [ ]
                                  [ Fa.i [Fa.Solid.AngleDown] [] ] ] ]
                      Card.content [ ]
                        [ Content.content   [ ]
                            [ counter model dispatch ] ] ]
#if (bridge)
                Card.card [ ]
                    [ Card.header [ ]
                        [ Card.Header.title [ ]
                              [ str "Clock" ]
                          Card.Header.icon [ ]
                              [ Icon.icon [ ]
                                  [ Fa.i [Fa.Solid.AngleDown] [] ] ] ]
                      Card.content [ ]
                        [ Content.content   [ ]
                            [ clock model dispatch ] ] ]

#endif
                               ] ]

let view (model : Model) (dispatch : Msg -> unit) =
    div [ ]
        [ navBrand
          Container.container [ ]
              [ Columns.columns [ ]
                  [ Column.column [ Column.Width (Screen.All, Column.Is3) ]
                      [ menu ]
                    Column.column [ Column.Width (Screen.All, Column.Is9) ]
                      [ breadcrump
                        hero
                        info
                        columns model dispatch ] ] ] ]
#elseif (layout == "fulma-cover")
let navBrand =
    Navbar.Brand.div [ ]
        [ Navbar.Item.a
            [ Navbar.Item.Props
                [ Href "https://safe-stack.github.io/"
                  Style [ BackgroundColor "#00d1b2" ] ] ]
            [ img [ Src "https://safe-stack.github.io/images/safe_top.png"
                    Alt "Logo" ] ] ]

let navMenu =
    Navbar.menu [ ]
        [ Navbar.End.div [ ]
            [ Navbar.Item.a [ ]
                [ str "Home" ]
              Navbar.Item.a [ ]
                [ str "Examples" ]
              Navbar.Item.a [ ]
                [ str "Documentation" ]
              Navbar.Item.div [ ]
                [ Button.a
                    [ Button.Size IsSmall
                      Button.Props [ Href "https://github.com/SAFE-Stack/SAFE-template" ] ]
                    [ Icon.icon [ ]
                        [ Fa.i [Fa.Brand.Github; Fa.FixedWidth] [] ]
                      span [ ] [ str "View Source" ] ] ] ] ]

let containerBox (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ ]
        [ Field.div [ Field.IsGrouped ]
            [ Control.p [ Control.IsExpanded ]
                [ Input.text
                    [ Input.Disabled true
                      Input.Value (show model) ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch Increment) ]
                    [ str "+" ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch Decrement) ]
                    [ str "-" ] ] ] ]

#if (bridge)
let containerBoxClock (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ ]
        [ Field.div [ Field.IsGrouped ]
            [ Control.p [ Control.IsExpanded ]
                [ Input.text
                    [ Input.Disabled true
                      Input.Value (showTime model) ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch (Outgoing Start)) ]
                    [ str "Start" ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch (Outgoing Pause)) ]
                    [ str "Pause" ] ] ] ]
#endif

let view (model : Model) (dispatch : Msg -> unit) =
    Hero.hero
        [ Hero.IsFullHeight
          Hero.IsBold ]
        [ Hero.head [ ]
            [ Navbar.navbar [  ]
                [ Container.container [ ]
                    [ navBrand
                      navMenu ] ] ]
          Hero.body [ ]
            [ Container.container
                [ Container.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                [ Columns.columns [ Columns.IsVCentered ]
                    [ Column.column
                        [ Column.Width (Screen.All, Column.Is5) ]
                        [ Image.image [ Image.Is4by3 ]
                            [ img [ Src "http://placehold.it/800x600" ] ] ]
                      Column.column
                       [ Column.Width (Screen.All, Column.Is5)
                         Column.Offset (Screen.All, Column.Is1) ]
                       [ Heading.h1 [ Heading.Is2 ]
                           [ str "Superhero Scaffolding" ]
                         Heading.h2
                           [ Heading.IsSubtitle
                             Heading.Is4 ]
                           [ safeComponents ]
                         containerBox model dispatch
#if (bridge)
                         containerBoxClock model dispatch
#endif
                          ] ] ] ]
          Hero.foot [ ]
            [ Container.container [ ]
                [ Tabs.tabs [ Tabs.IsCentered ]
                    [ ul [ ]
                        [ li [ ]
                            [ a [ ]
                                [ str "And this at the bottom" ] ] ] ] ] ] ]
#elseif (layout == "fulma-hero")
let navBrand =
    Navbar.Brand.div [ ]
        [ Navbar.Item.a
            [ Navbar.Item.Props [ Href "https://safe-stack.github.io/" ] ]
            [ img [ Src "https://safe-stack.github.io/images/safe_top.png"
                    Alt "Logo" ] ] ]

let navMenu =
    Navbar.menu [ ]
        [ Navbar.End.div [ ]
            [ Navbar.Item.a [ ]
                [ str "Home" ]
              Navbar.Item.a [ ]
                [ str "Examples" ]
              Navbar.Item.a [ ]
                [ str "Documentation" ]
              Navbar.Item.div [ ]
                [ Button.a
                    [ Button.Color IsWhite
                      Button.IsOutlined
                      Button.Size IsSmall
                      Button.Props [ Href "https://github.com/SAFE-Stack/SAFE-template" ] ]
                    [ Icon.icon [ ]
                        [ Fa.i [Fa.Brand.Github; Fa.FixedWidth] [] ]
                      span [ ] [ str "View Source" ] ] ] ] ]

let buttonBox (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ CustomClass "cta" ]
        [ Level.level [ ]
            [ Level.item [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch Increment) ]
                    [ str "+" ] ]

              Level.item [ ]
                [ p [ ] [ str (show model) ] ]

              Level.item [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch Decrement) ]
                    [ str "-" ] ] ] ]

#if (bridge)
let buttonBoxClock (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ CustomClass "cta" ]
        [ Level.level [ ]
            [ Level.item [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch (Outgoing Start)) ]
                    [ str "Start" ] ]

              Level.item [ ]
                [ p [ ] [ str (showTime model) ] ]

              Level.item [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch (Outgoing Pause)) ]
                    [ str "Pause" ] ] ] ]
#endif


let card icon heading body =
  Column.column [ Column.Width (Screen.All, Column.Is4) ]
    [ Card.card [ ]
        [ Card.image
            [ Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
            [ Icon.icon [ Icon.Size IsMedium
                          Icon.Props [ Style [ MarginTop "15px" ] ] ]
                [ Fa.i [icon; Fa.IconOption.Size Fa.Fa2x] [] ] ]
          Card.content [ ]
            [ Content.content [ ]
                [ h4 [ ] [ str heading ]
                  p [ ] [ str body ]
                  p [ ]
                    [ a [ Href "#" ]
                        [ str "Learn more" ] ] ] ] ] ]

let features =
    Columns.columns [ Columns.CustomClass "features" ]
        [ card Fa.Solid.Paw "Tristique senectus et netus et." "Purus semper eget duis at tellus at urna condimentum mattis. Non blandit massa enim nec. Integer enim neque volutpat ac tincidunt vitae semper quis. Accumsan tortor posuere ac ut consequat semper viverra nam."
          card Fa.Regular.IdCard "Tempor orci dapibus ultrices in." "Ut venenatis tellus in metus vulputate. Amet consectetur adipiscing elit pellentesque. Sed arcu non odio euismod lacinia at quis risus. Faucibus turpis in eu mi bibendum neque egestas cmonsu songue. Phasellus vestibulum lorem sed risus."
          card Fa.Solid.Rocket "Leo integer malesuada nunc vel risus." "Imperdiet dui accumsan sit amet nulla facilisi morbi. Fusce ut placerat orci nulla pellentesque dignissim enim. Libero id faucibus nisl tincidunt eget nullam. Commodo viverra maecenas accumsan lacus vel facilisis." ]

let intro =
    Column.column
        [ Column.CustomClass "intro"
          Column.Width (Screen.All, Column.Is8)
          Column.Offset (Screen.All, Column.Is2) ]
        [ h2 [ ClassName "title" ] [ str "Perfect for developers or designers!" ]
          br [ ]
          p [ ClassName "subtitle"] [ str "Vel fringilla est ullamcorper eget nulla facilisi. Nulla facilisi nullam vehicula ipsum a. Neque egestas congue quisque egestas diam in arcu cursus." ] ]

let tile title subtitle content =
    let details =
        match content with
        | Some c -> c
        | None -> nothing

    Tile.child [ ]
        [ Notification.notification [ Notification.Color IsWhite ]
            [ Heading.p [ ] [ str title ]
              Heading.p [ Heading.IsSubtitle ] [ str subtitle ]
              details ] ]

let content txts =
    Content.content [ ]
        [ for txt in txts -> p [ ] [ str txt ] ]

module Tiles =
    let hello = tile "Hello World" "What is up?" None

    let foo = tile "Foo" "Bar" None

    let third =
        tile
          "Third column"
          "With some content"
          (Some (content ["Lorem ipsum dolor sit amet, consectetur adipiscing elit. Proin ornare magna eros, eu pellentesque tortor vestibulum ut. Maecenas non massa sem. Etiam finibus odio quis feugiat facilisis."]))

    let verticalTop = tile "Vertical tiles" "Top box" None

    let verticalBottom = tile "Vertical tiles" "Bottom box" None

    let middle =
        tile
            "Middle box"
            "With an image"
            (Some (Image.image [ Image.Is4by3 ] [ img [ Src "http://bulma.io/images/placeholders/640x480.png"] ]))

    let wide =
        tile
            "Wide column"
            "Aligned with the right column"
            (Some (content ["Lorem ipsum dolor sit amet, consectetur adipiscing elit. Proin ornare magna eros, eu pellentesque tortor vestibulum ut. Maecenas non massa sem. Etiam finibus odio quis feugiat facilisis."]))

    let tall =
        tile
            "Tall column"
            "With even more content"
            (Some (content
                    ["Lorem ipsum dolor sit amet, consectetur adipiscing elit. Etiam semper diam at erat pulvinar, at pulvinar felis blandit. Vestibulum volutpat tellus diam, consequat gravida libero rhoncus ut. Morbi maximus, leo sit amet vehicula eleifend, nunc dui porta orci, quis semper odio felis ut quam."
                     "Suspendisse varius ligula in molestie lacinia. Maecenas varius eget ligula a sagittis. Pellentesque interdum, nisl nec interdum maximus, augue diam porttitor lorem, et sollicitudin felis neque sit amet erat. Maecenas imperdiet felis nisi, fringilla luctus felis hendrerit sit amet. Aenean vitae gravida diam, finibus dignissim turpis. Sed eget varius ligula, at volutpat tortor."
                     "Integer sollicitudin, tortor a mattis commodo, velit urna rhoncus erat, vitae congue lectus dolor consequat libero. Donec leo ligula, maximus et pellentesque sed, gravida a metus. Cras ullamcorper a nunc ac porta. Aliquam ut aliquet lacus, quis faucibus libero. Quisque non semper leo."]))

    let side =
        tile
            "Side column"
            "With some content"
            (Some (content ["Lorem ipsum dolor sit amet, consectetur adipiscing elit. Proin ornare magna eros, eu pellentesque tortor vestibulum ut. Maecenas non massa sem. Etiam finibus odio quis feugiat facilisis."]))

    let main =
        tile
            "Main column"
            "With some content"
            (Some (content ["Lorem ipsum dolor sit amet, consectetur adipiscing elit. Proin ornare magna eros, eu pellentesque tortor vestibulum ut. Maecenas non massa sem. Etiam finibus odio quis feugiat facilisis."]))


let sandbox =
    div [ ClassName "sandbox" ]
        [ Tile.ancestor [ ]
            [ Tile.parent [ ]
                [ Tiles.hello ]
              Tile.parent [ ]
                [ Tiles.foo ]
              Tile.parent [  ]
                [ Tiles.third ] ]
          Tile.ancestor [ ]
            [ Tile.tile [ Tile.IsVertical; Tile.Size Tile.Is8 ]
                [ Tile.tile [ ]
                    [ Tile.parent [ Tile.IsVertical ]
                        [ Tiles.verticalTop
                          Tiles.verticalBottom ]
                      Tile.parent [ ]
                        [ Tiles.middle ] ]
                  Tile.parent [ ]
                    [ Tiles.wide ] ]
              Tile.parent [ ]
                [ Tiles.tall ] ]
          Tile.ancestor [ ]
            [ Tile.parent [ ]
                [ Tiles.side ]
              Tile.parent [ Tile.Size Tile.Is8 ]
                [ Tiles.main ] ] ]

let footerContainer =
    Container.container [ ]
        [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
            [ p [ ]
                [ safeComponents ]
              p [ ]
                [ a [ Href "https://github.com/SAFE-Stack/SAFE-template" ]
                    [ Icon.icon [ ]
                        [ Fa.i [Fa.Brand.Github; Fa.FixedWidth] [] ] ] ] ] ]

let view (model : Model) (dispatch : Msg -> unit) =
    div [ ]
        [ Hero.hero
            [ Hero.Color IsPrimary
              Hero.IsMedium
              Hero.IsBold ]
            [ Hero.head [ ]
                [ Navbar.navbar [ ]
                    [ Container.container [ ]
                        [ navBrand
                          navMenu ] ] ]
              Hero.body [ ]
                [ Container.container [ Container.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.p [ ]
                        [ str "SAFE Template" ]
                      Heading.p [ Heading.IsSubtitle ]
                          [ safeComponents ] ] ] ]

          buttonBox model dispatch
#if (bridge)
          buttonBoxClock model dispatch
#endif

          Container.container [ ]
            [ features
              intro
              sandbox ]

          footer [ ClassName "footer" ]
            [ footerContainer ] ]
#elseif (layout == "fulma-landing")
let navBrand =
    Navbar.Brand.div [ ]
        [ Navbar.Item.a
            [ Navbar.Item.Props [ Href "https://safe-stack.github.io/" ]
              Navbar.Item.IsActive true ]
            [ img [ Src "https://safe-stack.github.io/images/safe_top.png"
                    Alt "Logo" ] ] ]

let navMenu =
    Navbar.menu [ ]
        [ Navbar.End.div [ ]
            [ Navbar.Item.a [ ]
                [ str "Home" ]
              Navbar.Item.a [ ]
                [ str "Examples" ]
              Navbar.Item.a [ ]
                [ str "Documentation" ]
              Navbar.Item.div [ ]
                [ Button.a
                    [ Button.Color IsWhite
                      Button.IsOutlined
                      Button.Size IsSmall
                      Button.Props [ Href "https://github.com/SAFE-Stack/SAFE-template" ] ]
                    [ Icon.icon [ ]
                        [ Fa.i [Fa.Brand.Github; Fa.FixedWidth] [] ]
                      span [ ] [ str "View Source" ] ] ] ] ]

let containerBox (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ ]
        [ Field.div [ Field.IsGrouped ]
            [ Control.p [ Control.IsExpanded ]
                [ Input.text
                    [ Input.Disabled true
                      Input.Value (show model) ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch Increment) ]
                    [ str "+" ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch Decrement) ]
                    [ str "-" ] ] ] ]

#if (bridge)
let containerBoxClock (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ ]
        [ Field.div [ Field.IsGrouped ]
            [ Control.p [ Control.IsExpanded ]
                [ Input.text
                    [ Input.Disabled true
                      Input.Value (showTime model) ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch (Outgoing Start)) ]
                    [ str "Start" ] ]
              Control.p [ ]
                [ Button.a
                    [ Button.Color IsPrimary
                      Button.OnClick (fun _ -> dispatch (Outgoing Pause)) ]
                    [ str "Pause" ] ] ] ]
#endif

let view (model : Model) (dispatch : Msg -> unit) =
    Hero.hero [ Hero.Color IsPrimary; Hero.IsFullHeight ]
        [ Hero.head [ ]
            [ Navbar.navbar [ ]
                [ Container.container [ ]
                    [ navBrand
                      navMenu ] ] ]

          Hero.body [ ]
            [ Container.container [ Container.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                [ Column.column
                    [ Column.Width (Screen.All, Column.Is6)
                      Column.Offset (Screen.All, Column.Is3) ]
                    [ Heading.p [ ]
                        [ str "SAFE Template" ]
                      Heading.p [ Heading.IsSubtitle ]
                        [ safeComponents ]
                      containerBox model dispatch
#if (bridge)
                      containerBoxClock model dispatch
#endif
                       ] ] ] ]
#else

let counter (model : Model) (dispatch : Msg -> unit) =
    Field.div [ Field.IsGrouped ]
        [ Control.p [ Control.IsExpanded ]
            [ Input.text
                [ Input.Disabled true
                  Input.Value (show model) ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch Increment) ]
                [ str "+" ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch Decrement) ]
                [ str "-" ] ] ]

#if (bridge)
let clock (model : Model) (dispatch : Msg -> unit) =
    Field.div [ Field.IsGrouped ]
        [ Control.p [ Control.IsExpanded ]
            [ Input.text
                [ Input.Disabled true
                  Input.Value (showTime model) ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch (Outgoing Start)) ]
                [ str "Start" ] ]
          Control.p [ ]
            [ Button.a
                [ Button.Color IsInfo
                  Button.OnClick (fun _ -> dispatch (Outgoing Pause)) ]
                [ str "Pause" ] ] ]
#endif

let column (model : Model) (dispatch : Msg -> unit) =
    Column.column
        [ Column.Width (Screen.All, Column.Is4)
          Column.Offset (Screen.All, Column.Is4) ]
        [ Heading.h3
            [ Heading.Modifiers [ Modifier.TextColor IsGrey ] ]
            [ str "Login" ]
          Heading.p
            [ Heading.Modifiers [ Modifier.TextColor IsGrey ] ]
            [ str "Please login to proceed." ]
          Box.box' [ ]
            [ figure [ Class "avatar" ]
                [ img [ Src "https://placehold.it/128x128" ] ]
              form [ ]
                [ Field.div [ ]
                    [ Control.div [ ]
                        [ Input.email
                            [ Input.Size IsLarge
                              Input.Placeholder "Your Email"
                              Input.Props [ AutoFocus true ] ] ] ]
                  Field.div [ ]
                    [ Control.div [ ]
                        [ Input.password
                            [ Input.Size IsLarge
                              Input.Placeholder "Your Password" ] ] ]
                  counter model dispatch
#if (bridge)
                  clock model dispatch
#endif
                  Field.div [ ]
                    [ Checkbox.checkbox [ ]
                        [ input [ Type "checkbox" ]
                          str "Remember me" ] ]
                  Button.button
                    [ Button.Color IsInfo
                      Button.IsFullWidth
                      Button.CustomClass "is-large is-block" ]
                    [ str "Login" ] ] ]
          Text.p [ Modifiers [ Modifier.TextColor IsGrey ] ]
            [ a [ ] [ str "Sign Up" ]
              str "\u00A0·\u00A0"
              a [ ] [ str "Forgot Password" ]
              str "\u00A0·\u00A0"
              a [ ] [ str "Need Help?" ] ]
          br [ ]
          Text.div [ Modifiers [   Modifier.TextColor IsGrey ] ]
            [ safeComponents ] ]

let view (model : Model) (dispatch : Msg -> unit) =
    Hero.hero
        [ Hero.Color IsSuccess
          Hero.IsFullHeight ]
        [ Hero.body [ ]
            [ Container.container
                [ Container.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                [ column model dispatch ] ] ]

#endif

//-:cnd:noEmit
#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

//+:cnd:noEmit
#if (reaction)
Program.mkSimple init update view
|> Program.withStream stream "msgs"
#else
Program.mkProgram init update view
#endif
#if (bridge)
|> Program.withBridgeConfig
    (
        Bridge.endpoint Socket.clock |>
        Bridge.withMapping Remote
    )
#endif
//-:cnd:noEmit
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
//+:cnd:noEmit
|> Program.run
