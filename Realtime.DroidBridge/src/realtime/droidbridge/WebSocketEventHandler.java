/*
 *  Copyright (C) 2012 Roderick Baier
 *  
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *  
 *  	http://www.apache.org/licenses/LICENSE-2.0
 *  
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License. 
 */

package realtime.droidbridge;


public interface WebSocketEventHandler
{
	public void onOpen();
	
	
	public void onMessage(WebSocketMessage message);
	
	
	//public void onError(IOException exception);
	
	
	public void onClose();

    public void onForcedClose();

	public void onPing();
	
	
	public void onPong();

    public void onException(Exception error);
}
