import { useCallback, useState, useEffect } from "react";
import MDXContent from '@theme/MDXContent';
import CodeBlock from '@theme/CodeBlock';
import './JwtTokenGeneratorClient.styles.css';

export default function JwtTokenGeneratorClient() {
	const [secret, setSecret] = useState<string>(generateRandomString(32));
	const [token, setToken] = useState<string>("");
	const [expDate, setExpDate] = useState<string>(getNextYearDateFormatted());
	const [namespace, setNamespace] = useState<string>("*");
	const [jobName, setJobName] = useState<string>("*");
	const [allocId, setAllocId] = useState<string>("*");
	const [capStatus, setCapStatus] = useState<boolean>(true);
	const [capFilesystemAccess, setCapFilesystemAccess] = useState<boolean>(true);
	const [capAppPoolLifecycle, setCapAppPoolLifecycle] = useState<boolean>(true);
	const [capScreenshots, setCapScreenshots] = useState<boolean>(true);
	const [capProcessDumps, setCapProcessDumps] = useState<boolean>(true);
	const [capDebug, setCapDebug] = useState<boolean>(true);

	useEffect(() => {
		generateToken();
	});

	return (
		<div style={{ display: "flex", flexDirection: "column", gap: "0.5em" }}>
			<h3>Configure the Plugin</h3>

			<MDXContent>
				JWT Tokens provide a more flexible way of securing the Management API.<br />

				Generate a random secret and fill it in here:
			</MDXContent>

			<div style={{ display: "flex", gap: "0.5em" }}>
				<label>JWT Secret:</label>
				<input type="text" value={secret} onChange={ev => setSecret(ev.target.value)} style={{ flex: 1 }}></input>
				<button onClick={regenerateSecret}>Regenerate</button>
			</div>

			<MDXContent>
				Specify the secret using the `--management-api-jwt-secret`-argument as shown:
			</MDXContent>

			<CodeBlock language="hcl">
				{
`plugin "nomad_iis" {
  args = [
    "--management-api-port=5004",
    # highlight-next-line
    "--management-api-jwt-secret=${secret}"
  ]
  config {
    enabled = true
  }
}`
				}
			</CodeBlock>

			<h3>Generate a JWT Token</h3>

			<div style={{ display: "flex", gap: "1em" }}>
				<div className="jwtTokenGenerator_row">
					<label className="label">Expiration Date:</label>
					<input className="control" type="date" value={expDate} onChange={ev => setExpDate(ev.target.value)} style={{ flex: 1 }}></input>
				</div>
			</div>

			<div style={{ display: "flex", gap: "1em" }}>
				<div style={{ flex: 1 }}>
					<h4>Limit to Job</h4>

					<div className="jwtTokenGenerator_row">
						<label className="label">Namespace:</label>
						<input className="control" type="text" value={namespace} onChange={ev => setNamespace(ev.target.value)} style={{ flex: 1 }}></input>
					</div>
					<div className="jwtTokenGenerator_row">
						<label className="label">Job Name:</label>
						<input className="control" type="text" value={jobName} onChange={ev => setJobName(ev.target.value)} style={{ flex: 1 }}></input>
					</div>
					<div className="jwtTokenGenerator_row">
						<label className="label">Alloc Id:</label>
						<input className="control" type="text" value={allocId} onChange={ev => setAllocId(ev.target.value)} style={{ flex: 1 }}></input>
					</div>
					<div className="jwtTokenGenerator_row">
						<span className="label"></span>
						<span className="control">Use <i>*</i> to allow all.</span>
					</div>
				</div>
				<div style={{ flex: 1 }}>
					<h4>Allowed Capabilities</h4>
					
					<div className="jwtTokenGenerator_row">
						<input id="cbCapability1" type="checkbox" checked={capStatus} onChange={ev => setCapStatus(ev.target.checked)}></input>
						<label htmlFor="cbCapability1">Status</label>
					</div>
					<div className="jwtTokenGenerator_row">
						<input id="cbCapability2" type="checkbox" checked={capFilesystemAccess} onChange={ev => setCapFilesystemAccess(ev.target.checked)}></input>
						<label htmlFor="cbCapability2">Filesystem Access</label>
					</div>
					<div className="jwtTokenGenerator_row">
						<input id="cbCapability3" type="checkbox" checked={capAppPoolLifecycle} onChange={ev => setCapAppPoolLifecycle(ev.target.checked)}></input>
						<label htmlFor="cbCapability3">Application Pool Lifecycle Management</label>
					</div>
					<div className="jwtTokenGenerator_row">
						<input id="cbCapability4" type="checkbox" checked={capScreenshots} onChange={ev => setCapScreenshots(ev.target.checked)}></input>
						<label htmlFor="cbCapability4">Screenshots</label>
					</div>
					<div className="jwtTokenGenerator_row">
						<input id="cbCapability5" type="checkbox" checked={capProcessDumps} onChange={ev => setCapProcessDumps(ev.target.checked)}></input>
						<label htmlFor="cbCapability5">Process Dumps</label>
					</div>
					<div className="jwtTokenGenerator_row">
						<input id="cbCapability6" type="checkbox" checked={capDebug} onChange={ev => setCapDebug(ev.target.checked)}></input>
						<label htmlFor="cbCapability6">Debug Information</label>
					</div>
				</div>
			</div>

			<h4>Your Token:</h4>
			<CodeBlock>{token}</CodeBlock>

			<p>
				You can now use this token in the following API-requests by providing it via the `Authorization` header in the form `Bearer &lt;token&gt;`.
			</p>
		</div>
	);

	function regenerateSecret() {
		setSecret(generateRandomString(32));
	}

	function generateRandomString(length) {
		const characters ='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';

		let result = '';
		const charactersLength = characters.length;
		
		for ( let i = 0; i < length; i++ ) {
			result += characters.charAt(Math.floor(Math.random() * charactersLength));
		}

		return result;
	}

	async function generateToken() {
		try {
			const now = new Date();

			const claims: any = {
				iss: "NomadIIS",
  				aud: "ManagementApi",
				iat: Math.round(now.getTime() / 1000),
				exp: Math.round(Date.parse(expDate) / 1000),
				capabilities: []
			};

			if (namespace)
				claims.namespace = namespace;
			if (jobName)
				claims.jobName = jobName;
			if (allocId)
				claims.allocId = allocId;

			if (capStatus)
				claims.capabilities.push("Status");
			if (capFilesystemAccess)
				claims.capabilities.push("FilesystemAccess");
			if (capAppPoolLifecycle)
				claims.capabilities.push("AppPoolLifecycle");
			if (capScreenshots)
				claims.capabilities.push("Screenshots");
			if (capProcessDumps)
				claims.capabilities.push("ProcDump");
			if (capDebug)
				claims.capabilities.push("Debug");

			console.log(claims);

			const t = await createToken(claims, secret);

			setToken(t);
		}
		catch (ex: any) {
			setToken(`Error: ${ex}`);
		}
	}

	async function createToken(payload, key) {
		var header = { typ: 'JWT', alg: 'HS256' };

		var segments = [];
		segments.push(encodeBase64Url(JSON.stringify(header)));
		segments.push(encodeBase64Url(JSON.stringify(payload)));

		var footer = await sign(segments.join('.'), key);

		segments.push(footer);

		return segments.join('.');
	}

	async function sign(data: any, secret: string) {
		var encoder = new TextEncoder();
		const encodedSecret = encoder.encode(secret);

		const cryptoKey = await window.crypto.subtle.importKey(
			"raw", //can be "jwk" or "raw"
			encodedSecret,
			{   //this is the algorithm options
				name: "HMAC",
				hash: { name: "SHA-256" }, //can be "SHA-1", "SHA-256", "SHA-384", or "SHA-512"
				//length: 256, //optional, if you want your key length to differ from the hash function's block length
			},
			true, //whether the key is extractable (i.e. can be used in exportKey)
			["sign", "verify"] //can be any combination of "sign" and "verify"
		);

		var encodedData = encoder.encode(data);

		const token = await window.crypto.subtle.sign(
			{
				name: "HMAC",
			},
			cryptoKey,
			encodedData
		);

		var u8 = new Uint8Array(token);
		var b64encoded = encodeBase64Url(String.fromCharCode(...u8));

		return b64encoded;
	}

	function encodeBase64Url(data) {
		return btoa(data).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
	}

	function getNextYearDateFormatted() {
		const now = new Date();
		const nextYear = new Date(now.setFullYear(now.getFullYear() + 1));

		return nextYear.toISOString().split('T')[0];
	}
}
