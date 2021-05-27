﻿/*
Copyright 2015 Pim de Witte All Rights Reserved.
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Speckle.Core.Kits;
using Speckle.Core.Logging;
using Unity.EditorCoroutines.Editor;
using UnityEngine;

namespace Speckle.ConnectorUnity {
    /// Author: Pim de Witte (pimdewitte.com) and contributors, https://github.com/PimDeWitte/UnityMainThreadDispatcher
    /// <summary>
    /// A thread-safe class which holds a queue with actions to execute on the next Update() method. It can be used to make calls to the main thread for
    /// things such as UI Manipulation in Unity. It was developed for use in combination with the Firebase Unity plugin, which uses separate threads for event handling
    /// </summary>
    ///
    [ExecuteAlways]
    public class Dispatcher : MonoBehaviour {

        private static readonly Queue<Action> _executionQueue = new Queue<Action>( );

        private static Dispatcher _instance;

        EditorCoroutine m_LoggerCoroutine;

        public static bool Ownerless { get; set; }

        public static Dispatcher Instance { get; set; }
        // public static Dispatcher Instance {
        //     get {
        //         if ( _instance == null )
        //             throw new Exception( "Could not find the Dispatcher object. Please ensure you have added a Dispatcher object with this script to your scene." );
        //
        //         return _instance;
        //     }
        //     set => _instance = value;
        // }

        private void OnEnable( )
            {
                Instance = this;
            }

        void Awake( )
            {
                Instance = this;
                Setup.Init( Applications.Unity );

                if ( _instance == null ) {
                    _instance = this;
                    if ( Application.isPlaying )
                        DontDestroyOnLoad( this.gameObject );
                }
            }

        void OnDestroy( )
            {
                _instance = null;
            }

        public void Update( )
            {
                lock ( _executionQueue ) {
                    while ( _executionQueue.Count > 0 ) {
                        _executionQueue.Dequeue( ).Invoke( );
                    }
                }
            }

        /// <summary>
        /// Locks the queue and adds the IEnumerator to the queue
        /// </summary>
        /// <param name="action">IEnumerator function that will be executed from the main thread.</param>
        public void Enqueue( IEnumerator action )
            {
                lock ( _executionQueue ) {
                    if ( Application.isEditor ) {
                        Debug.Log( "Calling from Editor" );
                        _executionQueue.Enqueue( ( ) => {
                            if ( Ownerless )
                                EditorCoroutineUtility.StartCoroutineOwnerless( action );
                            else
                                EditorCoroutineUtility.StartCoroutine( action, this );
                        } );
                    } else {
                        Debug.Log( "Calling from Play" );
                        _executionQueue.Enqueue( ( ) => { StartCoroutine( action ); } );
                    }
                }
            }

        /// <summary>
        /// Locks the queue and adds the Action to the queue
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        public void Enqueue( Action action )
            {
                Enqueue( ActionWrapper( action ) );
            }

        /// <summary>
        /// Locks the queue and adds the Action to the queue, returning a Task which is completed when the action completes
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        /// <returns>A Task that can be awaited until the action completes</returns>
        public Task EnqueueAsync( Action action )
            {
                var tcs = new TaskCompletionSource<bool>( );

                void WrappedAction( )
                    {
                        try {
                            action( );
                            tcs.TrySetResult( true );
                        }
                        catch ( Exception ex ) {
                            tcs.TrySetException( ex );
                        }
                    }

                Enqueue( ActionWrapper( WrappedAction ) );
                return tcs.Task;
            }

        IEnumerator ActionWrapper( Action a )
            {
                a( );
                yield return null;
            }

    }
}