// Local synthetic config only; no database access or production requests.
import {createServer} from 'node:http';
import {handleCloud} from '../src/cloud.mjs';
const env={QC_CLOUD_ENABLED:'true',QC_CLOUD_SCHEMA:'3',QC_CLOUD_PROVIDERS:'google,facebook',ACCESS_DB:{},QC_FIREBASE_WEB_CONFIG:JSON.stringify({apiKey:'AIza'+'a'.repeat(35),projectId:'quiet-camp',authDomain:'quiet-camp.firebaseapp.com',appId:'1:123456:web:abcdef'})};
createServer(async(req,res)=>{const response=await handleCloud(new Request('https://quietcamp.test'+req.url),env);res.writeHead(response.status,Object.fromEntries(response.headers));res.end(await response.text());}).listen(4196,'127.0.0.1');
