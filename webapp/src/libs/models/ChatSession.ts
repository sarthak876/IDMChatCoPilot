// Copyright (c) Microsoft. All rights reserved.

import { IChatMessage } from './ChatMessage';

export interface IChatSession {
    id: string;
    title: string;
    systemDescription: string;
    memoryBalance: number;
}

export interface ICreateChatSessionResponse {
    chatSession: IChatSession;
    initialBotMessage: IChatMessage;
}
